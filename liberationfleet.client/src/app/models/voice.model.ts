export interface VoiceParticipant {
  sessionId: number;
  userId: number;
  username: string;
  chatRoomId: number;
  isMuted: boolean;
  isDeafened: boolean;
  isSpeaking: boolean;
  isServerMuted: boolean;
  joinedAt: string;
}

export interface VoiceRoomPresence {
  chatRoomId: number;
  participants: VoiceParticipant[];
}

export interface VoicePresenceSnapshot {
  success: boolean;
  message: string;
  rooms: VoiceRoomPresence[];
}

export interface VoiceJoinResponse {
  success: boolean;
  message: string;
  token?: string;
  wsUrl?: string;
  liveKitRoomName?: string;
  sessionId?: number;
  chatRoomId?: number;
  previousChatRoomId?: number | null;
}

export interface VoiceOperationResponse {
  success: boolean;
  message: string;
}

export interface VoiceDevicePreferences {
  inputDeviceId: string;
  outputDeviceId: string;
}
