export type AdultContentPreference = 'Block' | 'Ask' | 'Show';

export interface ContentPreferencesDto {
  adultContentPreference: AdultContentPreference;
}

export interface ContentPreferencesResponse {
  success: boolean;
  message: string;
  preferences?: ContentPreferencesDto;
}

export interface UpdateContentPreferencesRequest {
  adultContentPreference: AdultContentPreference;
  settingsPassword?: string;
}

export const ADULT_CONTENT_PREFERENCE_OPTIONS: {
  value: AdultContentPreference;
  label: string;
  description: string;
}[] = [
  {
    value: 'Block',
    label: 'Block',
    description: 'Hide all chats and forums marked 18+.'
  },
  {
    value: 'Ask',
    label: 'Ask',
    description: 'Show 18+ entries with blurred forum thumbnails. Confirm your age before opening.'
  },
  {
    value: 'Show',
    label: 'Show',
    description: 'Show 18+ content with an 18+ marker only.'
  }
];
