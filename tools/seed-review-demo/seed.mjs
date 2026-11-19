#!/usr/bin/env node
/**
 * Seeds a private review fleet with two private crews, E2EE keys, and showcase data.
 *
 * Usage:
 *   set LF_API_BASE=https://liberationfleet.org
 *   node seed.mjs
 *
 * Optional: LF_API_BASE=https://localhost:7xxx (dev API)
 */

import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ACCOUNTS, ORGS, REVIEWER_PRIMARY, SHARED_PASSWORD } from './accounts.mjs';
import * as cryptoLib from './crypto.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const API_BASE = (process.env.LF_API_BASE || 'https://liberationfleet.org').replace(/\/$/, '');
const VENMO_PLATFORM_ID = 3;

const sessions = new Map();

function log(step, detail = '') {
  const suffix = detail ? ` — ${detail}` : '';
  console.log(`[seed] ${step}${suffix}`);
}

function fail(message, body) {
  const detail = body ? `\n${typeof body === 'string' ? body : JSON.stringify(body, null, 2)}` : '';
  throw new Error(`${message}${detail}`);
}

async function api(path, { method = 'GET', token, body, okStatuses = [200] } = {}) {
  const headers = { Accept: 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  const text = await res.text();
  let json = null;
  if (text) {
    try {
      json = JSON.parse(text);
    } catch {
      json = text;
    }
  }

  if (!okStatuses.includes(res.status)) {
    fail(`${method} ${path} → HTTP ${res.status}`, json);
  }
  return json;
}

async function registerOrLogin(account) {
  const register = await api('/api/auth/register', {
    method: 'POST',
    body: {
      username: account.username,
      email: account.email,
      password: SHARED_PASSWORD,
      confirmPassword: SHARED_PASSWORD
    },
    okStatuses: [200, 400]
  });

  if (register?.success && register.token) {
    return register;
  }

  const alreadyRegistered = /already registered/i.test(register?.message || '');
  if (!alreadyRegistered && register && register.success === false) {
    // Still try login in case of race; include register detail if login also fails.
    log('warn', `register ${account.email}: ${register.message || JSON.stringify(register.errors || register)}`);
  }

  const login = await api('/api/auth/login', {
    method: 'POST',
    body: { usernameOrEmail: account.email, password: SHARED_PASSWORD },
    okStatuses: [200, 400]
  });
  if (!login?.success || !login.token) {
    fail(`Login failed for ${account.email}`, { register, login });
  }
  if (login.requiresMfa) {
    fail(`Account ${account.email} requires MFA — turn MFA off for review accounts.`);
  }
  return login;
}

async function provisionCrypto(session, recoveryPhrase) {
  const keyPair = await cryptoLib.generateIdentityKeyPair();
  const publicKey = await cryptoLib.exportPublicKeySpki(keyPair.publicKey);
  const backup = await cryptoLib.wrapPrivateKeyBackup(
    keyPair.privateKey,
    recoveryPhrase,
    cryptoLib.BACKUP_WRAP_RECOVERY_KEY
  );

  await api('/api/crypto/keys/public', {
    method: 'PUT',
    token: session.token,
    body: { identityPublicKey: publicKey, keyVersion: 1 }
  });
  const backupResult = await api('/api/crypto/keys/backup', {
    method: 'PUT',
    token: session.token,
    body: backup
  });
  if (backupResult && backupResult.success === false) {
    fail('Failed to save private key backup', backupResult);
  }

  session.privateKey = keyPair.privateKey;
  session.publicKey = publicKey;
}

async function ensureCryptoUnlocked(session, account) {
  if (session.privateKey) return;

  const backup = await api('/api/crypto/keys/backup', {
    token: session.token,
    okStatuses: [200, 404]
  });

  if (backup?.ciphertext) {
    try {
      const privateKey = await cryptoLib.unwrapPrivateKeyBackup(backup, account.recoveryPhrase);
      const publicKey = await cryptoLib.exportPublicKeyFromPrivateKey(privateKey);
      await api('/api/crypto/keys/public', {
        method: 'PUT',
        token: session.token,
        body: { identityPublicKey: publicKey, keyVersion: 1 }
      });
      session.privateKey = privateKey;
      session.publicKey = publicKey;
      log('crypto', `unlocked ${account.username}`);
      return;
    } catch (err) {
      log('warn', `unlock failed for ${account.username} (${err.message}); re-provisioning`);
    }
  }

  await provisionCrypto(session, account.recoveryPhrase);
  log('crypto', `provisioned keys for ${account.username}`);
}

async function updateProfile(session, account) {
  await api('/api/profile', {
    method: 'PUT',
    token: session.token,
    body: {
      username: account.username,
      email: account.email,
      clearLocation: true,
      inNeedOfAid: account.inNeedOfAid,
      emergencyLevel: account.emergencyLevel,
      peopleRepresentedCount: account.peopleRepresented,
      disabilityLevel: account.disabilityLevel,
      identityGroups: account.identityGroups,
      needsSurvivalAid: account.needsSurvivalAid,
      paymentPlatforms: [
        {
          platformId: VENMO_PLATFORM_ID,
          platform: 'Venmo',
          handle: account.venmo,
          isPreferred: true
        }
      ]
    }
  });
}

async function getMembership(session) {
  return api('/api/crews/membership', { token: session.token, okStatuses: [200, 404] });
}

async function createCrew(session, org) {
  const result = await api('/api/crews', {
    method: 'POST',
    token: session.token,
    body: {
      name: org.name,
      maxSize: org.maxSize,
      privacy: org.privacy,
      scope: org.scope,
      countryCode: null,
      allowedZipCodes: []
    }
  });
  if (!result?.success) fail(`Create crew ${org.name} failed`, result);
  return result;
}

async function ensureCrewKey(session, crewId) {
  if (session.crewKeys?.[crewId]) {
    return session.crewKeys[crewId];
  }

  const state = await api(`/api/crypto/crew-keys/${crewId}`, {
    token: session.token,
    okStatuses: [200, 404]
  }) || {};
  const publicKeys = await api(`/api/crypto/keys/public/crew/${crewId}`, { token: session.token }) || [];
  const publicByUser = new Map(publicKeys.map((k) => [k.userId, k]));

  let keyBytes;
  let keyVersion = state.latestKeyVersion || 1;

  if (state.myDistribution) {
    const author = publicByUser.get(state.myDistribution.wrappedByUserId);
    if (!author) fail('Missing wrapper public key for crew key');
    keyBytes = await cryptoLib.unwrapCrewKey(
      state.myDistribution.wrappedCrewKey,
      state.myDistribution.wrapNonce,
      author.identityPublicKey,
      session.privateKey
    );
    keyVersion = state.myDistribution.keyVersion;
  } else if ((state.latestKeyVersion ?? 0) > 0) {
    fail(`Crew ${crewId} has a key but none wrapped for ${session.account.username}`);
  } else {
    keyBytes = cryptoLib.generateCrewKeyBytes();
    keyVersion = 1;
    for (const pk of publicKeys) {
      const wrapped = await cryptoLib.wrapCrewKeyForUser(keyBytes, pk.identityPublicKey, session.privateKey);
      await api(`/api/crypto/crew-keys/${crewId}`, {
        method: 'PUT',
        token: session.token,
        body: {
          userId: pk.userId,
          keyVersion,
          wrappedCrewKey: wrapped.wrappedCrewKey,
          wrapNonce: wrapped.wrapNonce
        }
      });
    }
  }

  const aes = await cryptoLib.importCrewAesKey(keyBytes);
  session.crewKeys = session.crewKeys || {};
  session.crewKeys[crewId] = { bytes: keyBytes, aes, keyVersion };
  return session.crewKeys[crewId];
}

async function wrapCrewKeyForUser(session, crewId, targetUserId, targetPublicKey) {
  const material = await ensureCrewKey(session, crewId);
  const wrapped = await cryptoLib.wrapCrewKeyForUser(material.bytes, targetPublicKey, session.privateKey);
  await api(`/api/crypto/crew-keys/${crewId}`, {
    method: 'PUT',
    token: session.token,
    body: {
      userId: targetUserId,
      keyVersion: material.keyVersion,
      wrappedCrewKey: wrapped.wrappedCrewKey,
      wrapNonce: wrapped.wrapNonce
    }
  });
}

async function ensureFleetKey(session, fleetId) {
  if (session.fleetKeys?.[fleetId]) {
    return session.fleetKeys[fleetId];
  }

  const state = await api(`/api/crypto/fleet-keys/${fleetId}`, {
    token: session.token,
    okStatuses: [200, 404]
  }) || {};
  const publicKeys = await api(`/api/crypto/keys/public/fleet/${fleetId}`, { token: session.token }) || [];
  const publicByUser = new Map(publicKeys.map((k) => [k.userId, k]));

  let keyBytes;
  let keyVersion = state.latestKeyVersion || 1;

  if (state.myDistribution) {
    const author = publicByUser.get(state.myDistribution.wrappedByUserId);
    keyBytes = await cryptoLib.unwrapFleetKey(
      state.myDistribution.wrappedFleetKey,
      state.myDistribution.wrapNonce,
      author.identityPublicKey,
      session.privateKey
    );
    keyVersion = state.myDistribution.keyVersion;
  } else if ((state.latestKeyVersion ?? 0) > 0) {
    fail(`Fleet ${fleetId} has a key but none wrapped for ${session.account.username}`);
  } else {
    keyBytes = cryptoLib.generateCrewKeyBytes();
    keyVersion = 1;
    for (const pk of publicKeys) {
      const wrapped = await cryptoLib.wrapFleetKeyForUser(keyBytes, pk.identityPublicKey, session.privateKey);
      await api(`/api/crypto/fleet-keys/${fleetId}`, {
        method: 'PUT',
        token: session.token,
        body: {
          userId: pk.userId,
          keyVersion,
          wrappedFleetKey: wrapped.wrappedFleetKey,
          wrapNonce: wrapped.wrapNonce
        }
      });
    }
  }

  const aes = await cryptoLib.importCrewAesKey(keyBytes);
  session.fleetKeys = session.fleetKeys || {};
  session.fleetKeys[fleetId] = { bytes: keyBytes, aes, keyVersion };
  return session.fleetKeys[fleetId];
}

async function wrapFleetKeyForUser(session, fleetId, targetUserId, targetPublicKey) {
  const material = await ensureFleetKey(session, fleetId);
  const wrapped = await cryptoLib.wrapFleetKeyForUser(material.bytes, targetPublicKey, session.privateKey);
  await api(`/api/crypto/fleet-keys/${fleetId}`, {
    method: 'PUT',
    token: session.token,
    body: {
      userId: targetUserId,
      keyVersion: material.keyVersion,
      wrappedFleetKey: wrapped.wrappedFleetKey,
      wrapNonce: wrapped.wrapNonce
    }
  });
}

function extractRuleIds(payload) {
  const list = payload?.items || payload?.rules || payload?.publicRules || [];
  return list.map((r) => r.id).filter((id) => Number.isFinite(id));
}

async function joinCrewWithCode(applicant, captain, joinCode, crewId) {
  let rules = await api(`/api/crews/public-rules?joinCode=${encodeURIComponent(joinCode)}`, {
    token: applicant.token,
    okStatuses: [200, 400]
  });
  let ruleIds = extractRuleIds(rules);
  if (!ruleIds.length) {
    rules = await api(`/api/crews/${crewId}/public-rules`, { token: applicant.token });
    ruleIds = extractRuleIds(rules);
  }
  if (!ruleIds.length) fail('No public rules found for join');

  const join = await api('/api/crews/join-request', {
    method: 'POST',
    token: applicant.token,
    body: { joinCode, acceptedRuleIds: ruleIds }
  });
  if (!join?.success) fail(`Join request failed for ${applicant.account.username}`, join);
  const proposalId = join.proposalId;
  log('join', `${applicant.account.username} → proposal ${proposalId}`);

  // Captain wraps crew key for applicant, then approves.
  // Prefer session public key (GetPublicKey may 404 until membership exists).
  let applicantPublicKey = applicant.publicKey;
  if (!applicantPublicKey) {
    const applicantPub = await api(`/api/crypto/keys/public/${applicant.userId}`, {
      token: captain.token,
      okStatuses: [200, 404]
    });
    applicantPublicKey = applicantPub?.identityPublicKey;
  }
  if (!applicantPublicKey) fail(`No public key for applicant ${applicant.account.username}`);
  await wrapCrewKeyForUser(captain, crewId, applicant.userId, applicantPublicKey);

  const vote = await api(`/api/proposals/${proposalId}/vote`, {
    method: 'POST',
    token: captain.token,
    body: { vote: 'approve' }
  });
  if (!vote?.success) fail(`Approve join failed for ${applicant.account.username}`, vote);

  // Extra yes votes if crew already has more members (majority)
  for (const other of sessions.values()) {
    if (other.userId === captain.userId || other.userId === applicant.userId) continue;
    if (other.account.crew !== captain.account.crew) continue;
    const membership = await getMembership(other);
    if (!membership?.hasCrew || membership.crewId !== crewId) continue;
    await api(`/api/proposals/${proposalId}/vote`, {
      method: 'POST',
      token: other.token,
      body: { vote: 'approve' },
      okStatuses: [200, 400]
    });
  }

  // Payment platforms only attach once the user is in a crew.
  await updateProfile(applicant, applicant.account);
}

async function sendChat(session, roomId, crewId, body) {
  const material = await ensureCrewKey(session, crewId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    body,
    authorDisplayName: session.account.displayName,
    attachments: []
  });
  const result = await api(`/api/chats/rooms/${roomId}/messages`, {
    method: 'POST',
    token: session.token,
    body: {
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext,
      keyVersion: material.keyVersion,
      notificationPreview: body.slice(0, 80),
      mentionedUserIds: [],
      isAnonymous: false
    }
  });
  if (!result?.success) fail('Send chat failed', result);
}

async function sendFleetChat(session, roomId, fleetId, body) {
  const material = await ensureFleetKey(session, fleetId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    body,
    authorDisplayName: session.account.displayName,
    attachments: []
  });
  const result = await api(`/api/chats/rooms/${roomId}/messages`, {
    method: 'POST',
    token: session.token,
    body: {
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext,
      keyVersion: material.keyVersion,
      notificationPreview: body.slice(0, 80),
      mentionedUserIds: [],
      isAnonymous: false
    }
  });
  if (!result?.success) fail('Send fleet chat failed', result);
}

function giftDtoId(gift) {
  const id = gift?.giftId ?? gift?.id ?? gift?.GiftId ?? 0;
  return Number(id) || 0;
}

/** Match GiftLogCryptoService.encryptAndStoreEntry / server FormatMessage after verify. */
async function encryptGiftLogEntry(session, crewId, entry) {
  if (!entry?.id) return;
  const material = await ensureCrewKey(session, crewId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    message: entry.message,
    giverName: entry.giverName,
    recipientName: entry.recipientName,
    middlemanName: entry.middlemanName ?? null,
    platform: entry.platform
  });
  const result = await api('/api/crypto/content', {
    method: 'PUT',
    token: session.token,
    body: {
      contentType: 'GiftLogEntry',
      resourceId: String(entry.id),
      crewId,
      keyVersion: material.keyVersion,
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext
    },
    okStatuses: [200, 400]
  });
  if (!result?.success) {
    fail(`Encrypt gift-log #${entry.id} failed`, result);
  }
}

function lotMessage(giverName, amount, itemTitle, recipientName) {
  const amountText = Number.isInteger(amount) ? String(amount) : String(amount).replace(/\.?0+$/, '');
  const item = (itemTitle || 'an offering').trim();
  return `${giverName} gave $${amountText} in ${item} to ${recipientName} via the Library of Things`;
}

function questMessage(contributorUsername, recipientUsername, amount, itemTitle) {
  const amountText = Number.isInteger(amount) ? String(amount) : String(amount).replace(/\.?0+$/, '');
  const title = (itemTitle || 'a quest').trim();
  return `${contributorUsername} gifted ${recipientUsername} $${amountText} worth of ${title}`;
}

function extractItemTitleFromLotMessage(message) {
  // "… gave $N in TITLE to NAME via the Library of Things"
  const match = String(message || '').match(/\bgave \$[\d.]+ in (.+) to .+ via the Library of Things\b/i);
  return match?.[1]?.trim() || '';
}

async function encryptLotContributionGift(session, crewId, gift, { quest = false } = {}) {
  const id = giftDtoId(gift);
  if (!id) {
    log('warn', `LoT/quest gift DTO missing giftId (server may still be returning 0 pre-SaveChanges)`);
    return false;
  }
  const giverName = gift.contributorUsername;
  const recipientName = gift.recipientUsername || 'Crewmate';
  const message = quest
    ? questMessage(giverName, recipientName, gift.amount, gift.itemTitle)
    : lotMessage(giverName, gift.amount, gift.itemTitle, recipientName);
  await encryptGiftLogEntry(session, crewId, {
    id,
    message,
    giverName,
    recipientName,
    middlemanName: null,
    platform: 'Library of Things'
  });
  return true;
}

/**
 * Peer Venmo gifts are first encrypted by the recipient (verify flow). Prefer recipient as author.
 */
async function resyncPeerGiftLogEncryption(crewMembers, crewId) {
  const viewer = crewMembers[0];
  const giftLog = await api('/api/gifts/log?limit=100', {
    token: viewer.token,
    okStatuses: [200, 400]
  });
  const items = (giftLog?.items || []).filter((entry) => {
    const platform = entry?.platform || '';
    const msg = entry?.message || '';
    if (/library of things/i.test(platform) || /via the Library of Things/i.test(msg)) return false;
    if (/gifted .+ \$[\d.]+ worth of /i.test(msg)) return false;
    return Boolean(msg) && /via Venmo/i.test(msg);
  });
  let updated = 0;
  for (const entry of items) {
    // recordAndVerifyGift encrypts as recipient; fall back to giver if needed.
    const authors = [
      crewMembers.find((m) => m.userId === entry.recipientId),
      crewMembers.find((m) => m.userId === entry.giverId)
    ].filter(Boolean);
    let ok = false;
    for (const author of authors) {
      try {
        await encryptGiftLogEntry(author, crewId, {
          ...entry,
          giverName: entry.giverName || authors.find((a) => a.userId === entry.giverId)?.account?.username,
          recipientName: entry.recipientName || authors.find((a) => a.userId === entry.recipientId)?.account?.username,
          platform: entry.platform || 'Venmo'
        });
        ok = true;
        updated += 1;
        break;
      } catch (err) {
        // try next author
        if (author === authors[authors.length - 1]) {
          log('warn', `gift-log re-encrypt #${entry.id}: ${err.message}`);
        }
      }
    }
    if (!ok && !authors.length) {
      log('warn', `gift-log re-encrypt #${entry.id}: no crew author`);
    }
  }
  log('gift-log', `re-encrypted ${updated}/${items.length} peer-payment entries`);
}

/**
 * Encrypt LoT / quest rows with the real client display strings.
 * Works around older APIs that returned giftId: 0 before identity SaveChanges.
 */
async function resyncLibraryGiftLogEncryption(crewMembers, crewId, questTitles = []) {
  const viewer = crewMembers[0];
  const giftLog = await api('/api/gifts/log?limit=100', {
    token: viewer.token,
    okStatuses: [200, 400]
  });
  const questTitleSet = new Set(questTitles.map((t) => t.trim().toLowerCase()).filter(Boolean));
  const items = (giftLog?.items || []).filter((entry) => {
    const platform = entry?.platform || '';
    const msg = entry?.message || '';
    return /library of things/i.test(platform) || /via the Library of Things/i.test(msg);
  });

  let updated = 0;
  for (const entry of items) {
    const giver = crewMembers.find((m) => m.userId === entry.giverId);
    const recipient = crewMembers.find((m) => m.userId === entry.recipientId);
    const author = giver || recipient;
    if (!author) continue;

    const giverName = entry.giverName || giver?.account?.username || 'Crewmate';
    const recipientName = entry.recipientName || recipient?.account?.username || 'Crewmate';
    const itemTitle = extractItemTitleFromLotMessage(entry.message) || 'an offering';
    const isQuest = questTitleSet.has(itemTitle.toLowerCase())
      || /gifted .+ \$[\d.]+ worth of /i.test(entry.message || '');

    const message = isQuest
      ? questMessage(giverName, recipientName, entry.amount, itemTitle)
      : lotMessage(giverName, entry.amount, itemTitle, recipientName);

    try {
      await encryptGiftLogEntry(author, crewId, {
        id: entry.id,
        message,
        giverName,
        recipientName,
        middlemanName: null,
        platform: 'Library of Things'
      });
      updated += 1;
      log(isQuest ? 'quest-gift' : 'lot-gift', message);
    } catch (err) {
      log('warn', `library gift-log encrypt #${entry.id}: ${err.message}`);
    }
  }
  log('gift-log', `encrypted ${updated}/${items.length} Library of Things entries`);
}

async function recordAndVerifyGift(giver, recipient, amount) {
  const membership = await getMembership(giver);
  const platforms = await api('/api/crews/payment-platforms', { token: giver.token });
  const venmo = (platforms || []).find((p) => /venmo/i.test(p.name));
  if (!venmo) fail('Venmo crew payment platform missing — update profiles first');

  const recorded = await api('/api/gifts', {
    method: 'POST',
    token: giver.token,
    body: {
      amount,
      paymentPlatformId: venmo.id,
      recipientId: recipient.userId,
      middlemanId: null,
      completingGiftId: null
    }
  });
  if (!recorded?.success || !recorded.entry?.id) fail('Record gift failed', recorded);

  const giftId = recorded.entry.id;
  const verified = await api(`/api/gifts/${giftId}/verify`, {
    method: 'POST',
    token: recipient.token,
    body: { action: 'confirmReceived' }
  });
  if (!verified?.success) fail(`Verify gift ${giftId} failed`, verified);

  // App encrypts the verified DTO (FormatMessage), not a freeform note.
  const entry = verified.entry || recorded.entry;
  await encryptGiftLogEntry(recipient, membership.crewId, entry);
  log('gift', entry.message || `${giver.account.username} → ${recipient.account.username} $${amount}`);
  return giftId;
}

async function createOffering(session, crewId, offering) {
  const material = await ensureCrewKey(session, crewId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    title: offering.title,
    description: offering.description,
    authorDisplayName: session.account.username,
    attachments: [],
    thumbnailResourceId: null
  });
  const result = await api('/api/library/offerings', {
    method: 'POST',
    token: session.token,
    body: {
      title: offering.title,
      descriptionPreview: offering.description.slice(0, 180),
      categoryIds: offering.categoryIds,
      valuePerUnit: offering.valuePerUnit,
      unitLabel: offering.unitLabel,
      quantity: offering.quantity,
      quantityNotApplicable: offering.quantityNotApplicable ?? false,
      stockTier1: offering.stockTier1 ?? null,
      stockTier2: offering.stockTier2 ?? null,
      stockTier3: offering.stockTier3 ?? null,
      stockTier4: offering.stockTier4 ?? null,
      stockTier5: offering.stockTier5 ?? null,
      stockTier6: offering.stockTier6 ?? null,
      minimumViewerTier: 1,
      countryCode: null,
      allowedZipCodes: [],
      thumbnailResourceId: null,
      kind: offering.kind,
      fulfillmentMode: offering.fulfillmentMode,
      visibility: offering.visibility,
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext,
      keyVersion: material.keyVersion
    }
  });
  if (!result?.success) fail(`Create offering ${offering.title} failed`, result);
  log('library', offering.title);
  return result;
}

async function createQuest(session, crewId, quest) {
  const material = await ensureCrewKey(session, crewId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    title: quest.title,
    details: quest.details
  });
  const result = await api('/api/library/tasks', {
    method: 'POST',
    token: session.token,
    body: {
      title: quest.title,
      details: quest.details,
      value: quest.value,
      hasDeadline: false,
      deleteOnCompletion: false,
      isRecurring: false,
      frequency: 'None',
      timeSpecific: false,
      isSpaced: false,
      interval: 1,
      daySpecific: false,
      weekDays: [],
      monthDays: [],
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext,
      keyVersion: material.keyVersion
    }
  });
  if (!result?.success) fail(`Create quest ${quest.title} failed`, result);
  log('quest', quest.title);
  return result;
}

async function firstUnitId(session, offeringId) {
  if (!offeringId) return null;
  const units = await api(`/api/library/offerings/${offeringId}/units`, {
    token: session.token,
    okStatuses: [200, 400]
  });
  const items = units?.items || units?.units || [];
  if (items[0]?.unitId || items[0]?.id) return items[0].unitId || items[0].id;
  if (Array.isArray(units) && units[0]) return units[0].unitId || units[0].id;
  return null;
}

async function encryptLotGiftDto(session, crewId, gift, recipientName) {
  const id = giftDtoId(gift);
  if (!id) {
    log('warn', `LoT gift DTO missing giftId — will backfill from gift log`);
    return false;
  }
  const resolvedRecipient = recipientName || gift.recipientUsername || 'Crewmate';
  const message = lotMessage(gift.contributorUsername, gift.amount, gift.itemTitle, resolvedRecipient);
  await encryptGiftLogEntry(session, crewId, {
    id,
    message,
    giverName: gift.contributorUsername,
    recipientName: resolvedRecipient,
    middlemanName: null,
    platform: 'Library of Things'
  });
  log('lot-gift', message);
  return true;
}

async function encryptCompletedLotGifts(session, crewId, completed) {
  // Mirrors library-request-detail / unit-detail after complete/acquire.
  // Contribution / reception / completer gifts are peer-to-peer (recipientUsername).
  if (completed?.contributionGift) {
    await encryptLotGiftDto(
      session,
      crewId,
      completed.contributionGift,
      completed.contributionGift.recipientUsername
    );
  }
  if (completed?.completerGift) {
    await encryptLotGiftDto(
      session,
      crewId,
      completed.completerGift,
      completed.completerGift.recipientUsername || 'Crewmate'
    );
  }
  if (completed?.receptionGift) {
    await encryptLotGiftDto(
      session,
      crewId,
      completed.receptionGift,
      completed.receptionGift.recipientUsername || 'Crewmate'
    );
  }
}

async function requestAndCompleteLot({
  requester,
  completer,
  crewId,
  unitId,
  purpose,
  quantity = 1
}) {
  const material = await ensureCrewKey(requester, crewId);
  const purposeEnc = await cryptoLib.encryptJson(material.aes, { purpose });
  const now = new Date();
  const start = new Date(now.getTime() - 3600_000).toISOString();
  const end = new Date(now.getTime() + 7 * 86400_000).toISOString();
  const created = await api(`/api/library/units/${unitId}/requests`, {
    method: 'POST',
    token: requester.token,
    body: {
      quantity,
      purposePreview: purpose.slice(0, 120),
      neededByStart: start,
      neededByEnd: end,
      timeZone: 'America/New_York',
      nonce: purposeEnc.nonce,
      ciphertext: purposeEnc.ciphertext,
      keyVersion: material.keyVersion
    }
  });
  if (!created?.success || !created.requestId) fail('Create LoT request failed', created);

  const completed = await api(`/api/library/requests/${created.requestId}/complete`, {
    method: 'POST',
    token: completer.token,
    okStatuses: [200, 400]
  });
  if (!completed?.success) fail('Complete LoT request failed', completed);

  await encryptCompletedLotGifts(completer, crewId, completed);
  return completed;
}

/** On-demand consumables / services use acquisition instead of a borrow request. */
async function recordLotAcquisition({
  acquirer,
  crewId,
  unitId,
  purpose,
  quantity = 1
}) {
  const material = await ensureCrewKey(acquirer, crewId);
  const purposeEnc = await cryptoLib.encryptJson(material.aes, { purpose });
  const completed = await api(`/api/library/units/${unitId}/acquisitions`, {
    method: 'POST',
    token: acquirer.token,
    body: {
      quantity,
      purposePreview: purpose.slice(0, 120),
      nonce: purposeEnc.nonce,
      ciphertext: purposeEnc.ciphertext,
      keyVersion: material.keyVersion
    }
  });
  if (!completed?.success) fail('LoT acquisition failed', completed);
  await encryptCompletedLotGifts(acquirer, crewId, completed);
  return completed;
}

async function completeQuest({ creator, completer, crewId, taskId, title }) {
  const marked = await api(`/api/library/tasks/${taskId}/complete-anytime`, {
    method: 'POST',
    token: completer.token,
    okStatuses: [200, 400]
  });
  if (!marked?.success) fail(`Quest complete-anytime failed for ${title}`, marked);

  const detail = await api(`/api/library/tasks/${taskId}`, { token: creator.token });
  const task = detail?.task || detail;
  let instanceIds = [...(task?.pendingConfirmationInstanceIds || [])];
  if (!instanceIds.length) {
    const instances = task?.instances || [];
    instanceIds = instances
      .filter((i) => /awaiting/i.test(String(i.status ?? '')) || i.status === 2)
      .map((i) => i.instanceId ?? i.id)
      .filter((id) => Number.isFinite(id));
  }
  if (!instanceIds.length) fail(`No quest instances to confirm for ${title}`, detail);

  const confirmed = await api(`/api/library/tasks/${taskId}/confirm`, {
    method: 'POST',
    token: creator.token,
    body: { instanceIds, titlePreview: title }
  });
  if (!confirmed?.success) fail(`Quest confirm failed for ${title}`, confirmed);

  for (const gift of confirmed.contributionGifts || []) {
    const ok = await encryptLotContributionGift(creator, crewId, {
      ...gift,
      itemTitle: title || gift.itemTitle
    }, { quest: true });
    if (ok) {
      log('quest-gift', questMessage(
        gift.contributorUsername,
        gift.recipientUsername,
        gift.amount,
        title || gift.itemTitle
      ));
    }
  }
}

async function createOpenProposal(session, crewId, title, description) {
  const material = await ensureCrewKey(session, crewId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    title,
    description,
    authorDisplayName: session.account.displayName,
    attachments: [],
    thumbnailResourceId: null
  });
  const result = await api('/api/proposals', {
    method: 'POST',
    token: session.token,
    body: {
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext,
      keyVersion: material.keyVersion,
      mentionedUserIds: [],
      title,
      description
    }
  });
  if (!result?.success) fail('Create proposal failed', result);
  log('proposal', title);
  return result.proposalId ?? result.id;
}

async function listRooms(session) {
  const rooms = await api('/api/chats/rooms', { token: session.token });
  return rooms.items || [];
}

async function listFleetRooms(session) {
  const rooms = await api('/api/fleets/current/chats', { token: session.token, okStatuses: [200, 400] });
  return rooms.items || [];
}

function isTextRoom(room) {
  return room.roomType === 0 || room.roomType === 'Text' || room.roomType === 'text';
}

async function voteProposal(session, proposalId) {
  await api(`/api/proposals/${proposalId}/vote`, {
    method: 'POST',
    token: session.token,
    body: { vote: 'approve' },
    okStatuses: [200, 400]
  });
}

async function leaveCrewIfAny(session) {
  const membership = await getMembership(session);
  if (!membership?.hasCrew) return;
  await api('/api/crews/leave', {
    method: 'POST',
    token: session.token,
    okStatuses: [200, 400]
  });
  session.crewKeys = {};
  log('crew', `${session.account.username} left ${membership.crewName}`);
}

async function buildCrew(crewKey) {
  const members = ACCOUNTS.filter((a) => a.crew === crewKey).map((a) => sessions.get(a.key));
  const captain = members.find((m) => m.account.role === 'captain');
  const org = ORGS[crewKey];

  let membership = await getMembership(captain);
  if (membership?.hasCrew) {
    try {
      await ensureCrewKey(captain, membership.crewId);
      log('crew', `already in ${membership.crewName} (${membership.joinCode})`);
    } catch (err) {
      log('warn', `crew key broken for ${org.name} (${err.message}); recreating crew`);
      // Everyone in this demo crew must leave so the captain can recreate.
      for (const m of members) {
        await leaveCrewIfAny(m);
      }
      membership = await getMembership(captain);
    }
  }

  if (!membership?.hasCrew) {
    await createCrew(captain, org);
    membership = await getMembership(captain);
    log('crew', `created ${org.name} (${membership.joinCode})`);
  }

  const crewId = membership.crewId;
  const joinCode = membership.joinCode;
  await ensureCrewKey(captain, crewId);
  await updateProfile(captain, captain.account);

  for (const mate of members.filter((m) => m.userId !== captain.userId)) {
    const mateMembership = await getMembership(mate);
    if (mateMembership?.hasCrew && mateMembership.crewId === crewId) {
      await ensureCrewKey(mate, crewId);
      await updateProfile(mate, mate.account);
      continue;
    }
    if (mateMembership?.hasCrew && mateMembership.crewId !== crewId) {
      log('warn', `${mate.account.username} already in another crew — skip`);
      continue;
    }
    await joinCrewWithCode(mate, captain, joinCode, crewId);
    await ensureCrewKey(mate, crewId);
    // Re-wrap for all after each join so new members get keys from captain
    for (const m of members) {
      const mm = await getMembership(m);
      if (!mm?.hasCrew || mm.crewId !== crewId) continue;
      if (!m.publicKey) fail(`Missing public key for ${m.account.username}`);
      await wrapCrewKeyForUser(captain, crewId, m.userId, m.publicKey);
    }
  }

  return { captain, members, crewId, joinCode, name: membership.crewName || org.name };
}

async function main() {
  console.log(`Seeding against ${API_BASE}`);

  // 1) Accounts + crypto + profiles
  for (const account of ACCOUNTS) {
    const auth = await registerOrLogin(account);
    const session = {
      account,
      token: auth.token,
      userId: auth.user?.id,
      privateKey: null,
      publicKey: null,
      crewKeys: {},
      fleetKeys: {}
    };
    if (!session.userId) {
      const payload = JSON.parse(Buffer.from(auth.token.split('.')[1], 'base64url').toString());
      const claim =
        payload.nameid
        || payload.sub
        || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
      session.userId = Number(claim);
    }
    if (!session.userId) fail(`Could not resolve user id for ${account.email}`);
    sessions.set(account.key, session);
    await ensureCryptoUnlocked(session, account);
    await updateProfile(session, account);
    log('user', `${account.displayName} <${account.email}> id=${session.userId}`);
  }

  // 2) Crews
  const cedar = await buildCrew('cedar');
  const river = await buildCrew('river');

  // 3) Fleet from Cedar, invite River
  const maya = sessions.get('maya');

  async function acceptFleetRules(session, fleetId) {
    const publicRules = await api(`/api/fleets/${fleetId}/public-rules`, {
      token: session.token,
      okStatuses: [200, 400]
    });
    const ruleIds = extractRuleIds(publicRules);
    if (!ruleIds.length) return;
    await api('/api/fleets/accept-rules', {
      method: 'POST',
      token: session.token,
      body: { acceptedRuleIds: ruleIds },
      okStatuses: [200, 400]
    });
  }

  let status = await api('/api/fleets/status', { token: maya.token });
  let fleetId = status.hasFleet ? status.fleetId : null;
  let fleetJoinCode = status.joinCode;

  if (!fleetId) {
    const created = await api('/api/fleets', {
      method: 'POST',
      token: maya.token,
      body: {
        name: ORGS.fleet.name,
        privacy: ORGS.fleet.privacy,
        scope: ORGS.fleet.scope,
        countryCode: null,
        allowedZipCodes: []
      }
    });
    if (!created?.success || !created.fleet?.id) fail('Create fleet failed', created);
    fleetId = created.fleet.id;
    fleetJoinCode = created.fleet.joinCode;
    log('fleet', `created ${ORGS.fleet.name} (${fleetJoinCode})`);
  } else {
    log('fleet', `already in fleet id=${fleetId}`);
  }

  await acceptFleetRules(maya, fleetId);
  status = await api('/api/fleets/status', { token: maya.token });
  fleetJoinCode = status.joinCode || fleetJoinCode;

  const current = await api('/api/fleets/current', { token: maya.token });
  if (!current?.success || !current.fleet?.id) fail('Get current fleet failed', current);
  fleetId = current.fleet.id;
  fleetJoinCode = current.fleet.joinCode || fleetJoinCode;
  await ensureFleetKey(maya, fleetId);

  // Invite River House
  const avery = sessions.get('avery');
  const riverMembership = await getMembership(avery);
  const invite = await api('/api/fleets/invite-crew', {
    method: 'POST',
    token: maya.token,
    body: { joinCode: riverMembership.joinCode },
    okStatuses: [200, 400]
  });
  if (invite?.success && invite.proposalId) {
    log('fleet-invite', `crew proposal ${invite.proposalId}`);
    // River crew approves invitation (majority)
    for (const m of river.members) {
      await voteProposal(m, invite.proposalId);
    }
    // After apply, a fleet join proposal appears — approve from Cedar (fleet scope)
    await new Promise((r) => setTimeout(r, 800));
    const fleetPending = await api('/api/proposals?status=Pending&scope=fleet', {
      token: maya.token,
      okStatuses: [200, 400]
    });
    for (const p of fleetPending?.items || []) {
      for (const m of cedar.members) {
        await voteProposal(m, p.id);
      }
      log('fleet-join', `approved fleet proposal ${p.id}`);
    }
  } else {
    log('fleet-invite', invite?.message || 'invite skipped / already joined');
  }

  // Wrap fleet keys for all Cedar + River members who can see fleet
  for (const m of [...cedar.members, ...river.members]) {
    try {
      const memberStatus = await api('/api/fleets/status', { token: m.token });
      if (!memberStatus.hasFleet || memberStatus.fleetId !== fleetId) continue;
      if (memberStatus.needsRuleAcceptance) {
        await acceptFleetRules(m, fleetId);
      }
      if (!m.publicKey) continue;
      await wrapFleetKeyForUser(maya, fleetId, m.userId, m.publicKey);
      await ensureFleetKey(m, fleetId);
    } catch (err) {
      log('warn', `fleet key for ${m.account.username}: ${err.message}`);
    }
  }

  async function startSeason(crewMembers, label) {
    const captainSession = crewMembers.find((m) => m.account.role === 'captain');
    const estimates = {
      maya: 120, jordan: 80, sam: 40, priya: 90, lee: 20,
      avery: 100, casey: 70, morgan: 35, devon: 85
    };
    for (const m of crewMembers) {
      const setup = await api('/api/season/setup', {
        method: 'POST',
        token: m.token,
        body: { estimatedMonthlyContribution: estimates[m.account.key] ?? 50 },
        okStatuses: [200, 400]
      });
      if (setup && setup.success === false) {
        log('warn', `season setup ${m.account.username}: ${setup.message}`);
      }
      const ready = await api('/api/season/ready', {
        method: 'POST',
        token: m.token,
        body: {},
        okStatuses: [200, 400]
      });
      if (ready && ready.success === false) {
        log('warn', `season ready ${m.account.username}: ${ready.message}`);
      }
    }

    const season = await api('/api/season/status', { token: captainSession.token, okStatuses: [200, 400] });
    if (season?.seasonStarted) {
      log('season', `${label}: already started`);
      return;
    }
    const started = await api('/api/season/propose-start', {
      method: 'POST',
      token: captainSession.token,
      okStatuses: [200, 400]
    });
    if (started?.success && started.proposalId) {
      for (const m of crewMembers) {
        await voteProposal(m, started.proposalId);
      }
      log('season', `${label}: start-season proposal ${started.proposalId} approved`);
    } else {
      log('season', `${label}: ${started?.message || season?.message || 'could not propose start'}`);
    }
  }

  await startSeason(cedar.members, 'Cedar Circle');
  await startSeason(river.members, 'River House');

  // 4) Showcase content — financial gifts use post-verify FormatMessage ciphertext
  await recordAndVerifyGift(sessions.get('jordan'), sessions.get('sam'), 40);
  await recordAndVerifyGift(sessions.get('priya'), sessions.get('lee'), 75);
  await recordAndVerifyGift(sessions.get('maya'), sessions.get('lee'), 50);
  await recordAndVerifyGift(sessions.get('devon'), sessions.get('morgan'), 25);

  // Library offerings + real request/complete / quest confirm → LoT gift-log format
  const categories = await api('/api/library/categories', { token: maya.token });
  const catList = categories.items || [];
  const toolsCat = catList.find((c) => /tool|hardware|home|household/i.test(c.name)) || catList[0];
  const foodCat = catList.find((c) => /produce|food|bakery/i.test(c.name)) || catList[1] || catList[0];
  const serviceCat = catList.find((c) => /service|care|skill|labor/i.test(c.name)) || catList[2] || catList[0];

  const mattress = await createOffering(maya, cedar.crewId, {
    title: 'Folding air mattress + pump',
    description: 'Queen inflatable mattress with electric pump. Borrow for guests or emergencies. Return clean and dry.',
    categoryIds: toolsCat ? [toolsCat.id] : [1],
    valuePerUnit: 60,
    unitLabel: 'item',
    quantity: 1,
    kind: 'Durable',
    fulfillmentMode: 'OnRequest',
    visibility: 'CrewOnly'
  });
  const produce = await createOffering(sessions.get('jordan'), cedar.crewId, {
    title: 'Weekend produce box',
    description: 'Extra CSA share — leafy greens, apples, and eggs. First come, pickup Saturday morning.',
    categoryIds: foodCat ? [foodCat.id] : [1],
    valuePerUnit: 25,
    unitLabel: 'box',
    quantity: 2,
    // Consumables are per-tier; seed stock on every tier so any priority can acquire.
    stockTier1: 2,
    stockTier2: 2,
    stockTier3: 2,
    stockTier4: 2,
    stockTier5: 2,
    stockTier6: 2,
    kind: 'Consumable',
    fulfillmentMode: 'OnDemand',
    visibility: 'CrewOnly'
  });
  await createOffering(sessions.get('priya'), cedar.crewId, {
    title: 'Resume + benefits paperwork help',
    description: 'Two-hour remote session helping with resumes, SNAP/Medicaid forms, or job apps. Sliding priority.',
    categoryIds: serviceCat ? [serviceCat.id] : [1],
    valuePerUnit: 40,
    unitLabel: 'session',
    quantity: 3,
    quantityNotApplicable: false,
    kind: 'Service',
    fulfillmentMode: 'OnRequest',
    visibility: 'FleetWide'
  });

  await createOffering(avery, river.crewId, {
    title: 'Cargo bike (half day)',
    description: 'Electric cargo bike for grocery / pharmacy runs within city limits. Helmet included.',
    categoryIds: toolsCat ? [toolsCat.id] : [1],
    valuePerUnit: 80,
    unitLabel: 'loan',
    quantity: 1,
    kind: 'Durable',
    fulfillmentMode: 'OnRequest',
    visibility: 'FleetWide'
  });

  const mattressUnitId = (mattress.unitIds && mattress.unitIds[0])
    || (mattress.UnitIds && mattress.UnitIds[0])
    || await firstUnitId(maya, mattress.offeringId);
  const produceUnitId = (produce.unitIds && produce.unitIds[0])
    || (produce.UnitIds && produce.UnitIds[0])
    || await firstUnitId(sessions.get('jordan'), produce.offeringId);
  log('library-ids', `mattress offering=${mattress.offeringId} unit=${mattressUnitId}; produce offering=${produce.offeringId} unit=${produceUnitId}`);

  if (mattressUnitId) {
    log('lot', `durable request/complete unit ${mattressUnitId}`);
    try {
      await requestAndCompleteLot({
        requester: sessions.get('lee'),
        completer: maya,
        crewId: cedar.crewId,
        unitId: mattressUnitId,
        purpose: 'Cousin visiting this weekend — needs a place to sleep two nights.'
      });
    } catch (err) {
      log('warn', `mattress LoT: ${err.message || err}`);
    }
  } else {
    log('warn', 'No mattress unit id — skipped LoT durable completion');
  }

  if (produceUnitId) {
    log('lot', `consumable acquisition unit ${produceUnitId}`);
    try {
      await recordLotAcquisition({
        acquirer: sessions.get('sam'),
        crewId: cedar.crewId,
        unitId: produceUnitId,
        purpose: 'Kids lunches this week — greens, fruit, eggs if still available.'
      });
    } catch (err) {
      log('warn', `produce LoT: ${err.message || err}`);
    }
  } else {
    log('warn', 'No produce unit id — skipped LoT consumable acquisition');
  }

  const pantryQuest = await createQuest(maya, cedar.crewId, {
    title: 'Stock the mutual-aid pantry shelf',
    details: 'Add rice, beans, and shelf-stable milk to the building pantry. Completion counts as a gift toward priority.',
    value: 30
  });
  const welcomeQuest = await createQuest(avery, river.crewId, {
    title: 'Build the River House welcome packet',
    details: 'Write a one-page guide for new crewmates: Venmo handles, LoT etiquette, and voting norms.',
    value: 20
  });

  if (pantryQuest?.taskId) {
    await completeQuest({
      creator: maya,
      completer: sessions.get('priya'),
      crewId: cedar.crewId,
      taskId: pantryQuest.taskId,
      title: 'Stock the mutual-aid pantry shelf'
    });
  }
  if (welcomeQuest?.taskId) {
    await completeQuest({
      creator: avery,
      completer: sessions.get('casey'),
      crewId: river.crewId,
      taskId: welcomeQuest.taskId,
      title: 'Build the River House welcome packet'
    });
  }

  const questTitles = [
    'Stock the mutual-aid pantry shelf',
    'Build the River House welcome packet'
  ];

  // Fix Venmo + LoT/quest ciphertext (including giftId:0 responses from older APIs).
  await resyncPeerGiftLogEncryption(cedar.members, cedar.crewId);
  await resyncPeerGiftLogEncryption(river.members, river.crewId);
  await resyncLibraryGiftLogEncryption(cedar.members, cedar.crewId, questTitles);
  await resyncLibraryGiftLogEncryption(river.members, river.crewId, questTitles);

  // Chat that references the real flows — only seed once so re-runs aren't repetitive.
  const rooms = await listRooms(maya);
  const cedarText = rooms.find((r) => isTextRoom(r) && /general/i.test(r.name || ''))
    || rooms.find((r) => isTextRoom(r));
  if (cedarText) {
    const existing = await api(`/api/chats/rooms/${cedarText.id}/messages?limit=5`, {
      token: maya.token,
      okStatuses: [200, 400]
    });
    const existingCount = (existing?.items || []).length;
    if (existingCount > 0) {
      log('chat', `Cedar General already has messages (${existingCount}+) — skipping re-seed`);
    } else {
      const thread = [
        [maya, 'Season is open. Lee and Sam are both marked in need — keep gift log confirmations current so priority stays fair.'],
        [sessions.get('jordan'), 'Venmo’d Sam $40 for groceries after my shift. Please confirm when it lands.'],
        [sessions.get('sam'), 'Confirmed. Also grabbed one of your produce boxes in LoT for the kids’ lunches — thanks.'],
        [sessions.get('priya'), 'Lee, I can cover the pharmacy co-pay ($75) today if you still need it.'],
        [sessions.get('lee'), 'Yes please. Confirming when it hits. Maya — requesting the air mattress for my cousin this weekend.'],
        [maya, 'Mattress handoff is done in LoT. Priya, pantry quest is yours whenever you can stock the shelf.'],
        [sessions.get('priya'), 'Pantry’s stocked — marked the quest complete for your confirmation.'],
        [maya, 'Confirmed. Gift log should show the Venmo gifts plus the LoT / quest contributions now.']
      ];
      for (const [who, text] of thread) {
        await sendChat(who, cedarText.id, cedar.crewId, text);
      }
      log('chat', 'Cedar General thread seeded');
    }
  } else {
    log('warn', 'No Cedar text room found for chat seed');
  }

  const riverRooms = await listRooms(avery);
  const riverText = riverRooms.find((r) => isTextRoom(r) && /general/i.test(r.name || ''))
    || riverRooms.find((r) => isTextRoom(r));
  if (riverText) {
    const existing = await api(`/api/chats/rooms/${riverText.id}/messages?limit=5`, {
      token: avery.token,
      okStatuses: [200, 400]
    });
    if ((existing?.items || []).length > 0) {
      log('chat', 'River General already has messages — skipping re-seed');
    } else {
      await sendChat(avery, riverText.id, river.crewId,
        'Devon logged $25 to Morgan for the clinic ride — Morgan, confirm in the gift log when you can.');
      await sendChat(sessions.get('morgan'), riverText.id, river.crewId,
        'Confirmed. Casey, thanks for drafting the welcome packet quest — Avery still needs to confirm completion.');
      await sendChat(sessions.get('casey'), riverText.id, river.crewId,
        'Packet’s in the shared folder. Cargo bike is listed fleet-wide if Cedar needs a grocery run.');
      log('chat', 'River General thread seeded');
    }
  }

  // Open proposal (leaves something pending for reviewers to vote on)
  await createOpenProposal(
    sessions.get('sam'),
    cedar.crewId,
    'Add a “childcare swap” text channel',
    'Proposal: create a childcare-swap chat room for last-minute coverage among Cedar parents and caregivers.'
  );

  // Fleet chat if available
  const fleetRooms = await listFleetRooms(maya);
  const fleetRoom = fleetRooms.find((r) => isTextRoom(r));
  if (fleetRoom) {
    try {
      await sendFleetChat(maya, fleetRoom.id, fleetId,
        'Harbor Mutual Aid: Cedar Circle + River House share LoT fleet-wide offerings and coordinate between crews here.');
    } catch (err) {
      log('warn', `fleet chat: ${err.message}`);
    }
  }

  // 5) Write credentials file
  const lines = [];
  lines.push('# Liberation Fleet — App Review demo credentials');
  lines.push('');
  lines.push(`API: ${API_BASE}`);
  lines.push(`Generated: ${new Date().toISOString()}`);
  lines.push('');
  lines.push('## Primary App Review account');
  const primary = ACCOUNTS.find((a) => a.key === REVIEWER_PRIMARY);
  lines.push(`- Email: ${primary.email}`);
  lines.push(`- Password: ${SHARED_PASSWORD}`);
  lines.push(`- Recovery phrase (12 words): ${primary.recoveryPhrase}`);
  lines.push('');
  lines.push('## Orgs');
  lines.push(`- Fleet (Private): ${ORGS.fleet.name} — join code ${fleetJoinCode}`);
  lines.push(`- Crew (Private): ${cedar.name} — join code ${cedar.joinCode}`);
  lines.push(`- Crew (Private): ${river.name} — join code ${river.joinCode}`);
  lines.push('');
  lines.push('## All accounts (same password)');
  lines.push(`Password for every account: \`${SHARED_PASSWORD}\``);
  lines.push('');
  for (const a of ACCOUNTS) {
    const s = sessions.get(a.key);
    lines.push(`### ${a.displayName} (\`${a.username}\`) — ${a.crew === 'cedar' ? cedar.name : river.name}${a.role === 'captain' ? ' · captain' : ''}`);
    lines.push(`- Email: ${a.email}`);
    lines.push(`- User id: ${s.userId}`);
    lines.push(`- Recovery phrase: ${a.recoveryPhrase}`);
    lines.push('');
  }
  lines.push('## What reviewers should open after sign-in (Maya)');
  lines.push('1. Unlock encryption with the recovery phrase above (or remember-on-device).');
  lines.push('2. Open **Cedar Circle** → chat **General** (seeded conversation).');
  lines.push('3. Open **Gift log** (verified mutual-aid gifts with encrypted notes).');
  lines.push('4. Open **Library of Things** (durable, consumable, service offerings + quest).');
  lines.push('5. Open **Proposals** (pending childcare-channel proposal to vote on).');
  lines.push('6. Open **Harbor Mutual Aid** fleet (second crew River House + fleet-wide LoT).');
  lines.push('');
  lines.push('Do not use these accounts for personal mutual aid. Demo-only.');

  const outPath = join(__dirname, 'CREDENTIALS.local.md');
  writeFileSync(outPath, lines.join('\n'), 'utf8');
  console.log(`\nWrote ${outPath}`);
  console.log('\nPrimary reviewer login:');
  console.log(`  ${primary.email}`);
  console.log(`  ${SHARED_PASSWORD}`);
  console.log(`  phrase: ${primary.recoveryPhrase}`);
}

main().catch((err) => {
  console.error('\nSeed failed:', err.message || err);
  process.exit(1);
});
