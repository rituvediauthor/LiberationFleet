import { Injectable, inject } from '@angular/core';
import { AdultContentPreference } from '../models/content-preference.model';
import { ContentPreferenceService } from './content-preference.service';

export type AdultContentResourceType = 'chat' | 'forum';

@Injectable({
  providedIn: 'root'
})
export class AdultContentService {
  private readonly contentPreferences = inject(ContentPreferenceService);
  private readonly sessionConsents = new Set<string>();

  get preference(): AdultContentPreference {
    return this.contentPreferences.preference;
  }

  resourceKey(type: AdultContentResourceType, id: number): string {
    return `${type}:${id}`;
  }

  shouldShowEntry(isAdultContent: boolean | undefined, preference = this.preference): boolean {
    if (!isAdultContent) {
      return true;
    }
    return preference !== 'Block';
  }

  shouldBlurThumbnail(isAdultContent: boolean | undefined, preference = this.preference): boolean {
    return !!isAdultContent && preference === 'Ask';
  }

  needsAgeGate(
    isAdultContent: boolean | undefined,
    resourceKey: string,
    preference = this.preference
  ): boolean {
    return !!isAdultContent && preference === 'Ask' && !this.sessionConsents.has(resourceKey);
  }

  grantConsent(resourceKey: string): void {
    this.sessionConsents.add(resourceKey);
  }

  hasConsented(resourceKey: string): boolean {
    return this.sessionConsents.has(resourceKey);
  }
}
