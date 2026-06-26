import { Injectable } from '@angular/core';
import { RuleDetail, RuleEncryptedPayload, RuleListItem } from '../../models/rule.model';
import { CryptoSessionService } from './crypto-session.service';
import { CryptoService } from './crypto.service';
import { ProposalCryptoService } from './proposal-crypto.service';

@Injectable({
  providedIn: 'root'
})
export class RuleCryptoService {
  constructor(
    private cryptoSession: CryptoSessionService,
    private cryptoService: CryptoService,
    private proposalCrypto: ProposalCryptoService
  ) {}

  async decryptRules(rules: RuleListItem[], crewId: number): Promise<RuleListItem[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return rules.map(rule => ({
        ...rule,
        title: '[Unlock encryption to view]',
        descriptionPreview: '[Unlock encryption to view]'
      }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(rules.map(rule => this.decryptRuleItem(rule, crewKey)));
  }

  async decryptDetail(rule: RuleDetail, crewId: number): Promise<RuleDetail> {
    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...rule,
        title: '[Unlock encryption to view]',
        description: '[Unlock encryption to view]'
      };
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return this.decryptRuleItem(rule, crewKey);
  }

  encryptRulePayload(
    crewId: number,
    payload: RuleEncryptedPayload
  ): Promise<{ nonce: string; ciphertext: string }> {
    return this.proposalCrypto.encryptProposalPayload(crewId, payload);
  }

  private async decryptRuleItem(rule: RuleListItem, crewKey: CryptoKey): Promise<RuleListItem> {
    if (!rule.hasEncryptedContent || !rule.encryptedPayload) {
      return rule;
    }

    try {
      const payload = await this.cryptoService.decryptJson<RuleEncryptedPayload>(
        crewKey,
        rule.encryptedPayload.nonce,
        rule.encryptedPayload.ciphertext
      );
      const description = payload.description ?? '';
      return {
        ...rule,
        title: payload.title,
        description,
        descriptionPreview: description.length > 160 ? `${description.slice(0, 160)}…` : description
      };
    } catch {
      return {
        ...rule,
        title: '[Unable to decrypt]',
        descriptionPreview: '[Unable to decrypt]'
      };
    }
  }
}
