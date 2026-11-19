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

async function recordAndVerifyGift(giver, recipient, amount, message) {
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
  const material = await ensureCrewKey(giver, membership.crewId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    message,
    giverName: giver.account.displayName,
    recipientName: recipient.account.displayName,
    middlemanName: null,
    platform: 'Venmo'
  });
  await api('/api/crypto/content', {
    method: 'PUT',
    token: giver.token,
    body: {
      contentType: 'GiftLogEntry',
      resourceId: String(giftId),
      crewId: membership.crewId,
      keyVersion: material.keyVersion,
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext
    }
  });

  const verified = await api(`/api/gifts/${giftId}/verify`, {
    method: 'POST',
    token: recipient.token,
    body: { action: 'confirmReceived' }
  });
  if (!verified?.success) fail(`Verify gift ${giftId} failed`, verified);
  log('gift', `${giver.account.username} → ${recipient.account.username} $${amount}`);
  return giftId;
}

async function createOffering(session, crewId, offering) {
  const material = await ensureCrewKey(session, crewId);
  const encrypted = await cryptoLib.encryptJson(material.aes, {
    title: offering.title,
    description: offering.description,
    authorDisplayName: session.account.displayName,
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
      stockTier1: null,
      stockTier2: null,
      stockTier3: null,
      stockTier4: null,
      stockTier5: null,
      stockTier6: null,
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

  // 4) Showcase content — Cedar
  const rooms = await listRooms(maya);
  const cedarText = rooms.find((r) => isTextRoom(r) && /general/i.test(r.name || ''))
    || rooms.find((r) => isTextRoom(r));
  if (cedarText) {
    await sendChat(maya, cedarText.id, cedar.crewId,
      'Welcome to Cedar Circle — we coordinate weekly mutual aid, Library of Things shares, and democratic crew votes here.');
    await sendChat(sessions.get('jordan'), cedarText.id, cedar.crewId,
      'I can drop off the spare air mattress this weekend. Logging it in Library of Things now.');
    await sendChat(sessions.get('sam'), cedarText.id, cedar.crewId,
      'Thanks everyone — groceries last week really helped. Confirming gifts in the gift log.');
    await sendChat(sessions.get('lee'), cedarText.id, cedar.crewId,
      'Still in a rough patch with medical costs. Grateful for the survival-threshold priority this season.');
  } else {
    log('warn', 'No Cedar text room found for chat seed');
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

  // Gifts (verified — requires active season)
  await recordAndVerifyGift(sessions.get('jordan'), sessions.get('sam'), 40, 'Groceries run — milk, rice, fruit');
  await recordAndVerifyGift(sessions.get('priya'), sessions.get('lee'), 75, 'Help with pharmacy co-pay');
  await recordAndVerifyGift(sessions.get('maya'), sessions.get('lee'), 50, 'Transit cards + shared dinner');
  await recordAndVerifyGift(sessions.get('devon'), sessions.get('morgan'), 25, 'River House: ride to clinic');

  // Library offerings
  const categories = await api('/api/library/categories', { token: maya.token });
  const catList = categories.items || [];
  const toolsCat = catList.find((c) => /tool|hardware|home|household/i.test(c.name)) || catList[0];
  const foodCat = catList.find((c) => /produce|food|bakery/i.test(c.name)) || catList[1] || catList[0];
  const serviceCat = catList.find((c) => /service|care|skill|labor/i.test(c.name)) || catList[2] || catList[0];

  await createOffering(maya, cedar.crewId, {
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
  await createOffering(sessions.get('jordan'), cedar.crewId, {
    title: 'Weekend produce box',
    description: 'Extra CSA share — leafy greens, apples, and eggs. First come, pickup Saturday morning.',
    categoryIds: foodCat ? [foodCat.id] : [1],
    valuePerUnit: 25,
    unitLabel: 'box',
    quantity: 2,
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

  await createQuest(maya, cedar.crewId, {
    title: 'Stock the mutual-aid pantry shelf',
    details: 'Add rice, beans, and shelf-stable milk to the building pantry. Completion counts as a gift toward priority.',
    value: 30
  });
  await createQuest(avery, river.crewId, {
    title: 'Build the River House welcome packet',
    details: 'Write a one-page guide for new crewmates: Venmo handles, LoT etiquette, and voting norms.',
    value: 20
  });

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
