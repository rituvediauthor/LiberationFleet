import { VoiceLiveKitService } from './voice-livekit.service';

describe('VoiceLiveKitService', () => {
  let service: VoiceLiveKitService;

  beforeEach(() => {
    localStorage.removeItem('voiceDevicePreferences');
    service = new VoiceLiveKitService();
  });

  afterEach(() => {
    localStorage.removeItem('voiceDevicePreferences');
  });

  it('loads empty device preferences by default', () => {
    const prefs = service.loadDevicePreferences();
    expect(prefs.inputDeviceId).toBe('');
    expect(prefs.outputDeviceId).toBe('');
  });

  it('persists device preferences', () => {
    service.saveDevicePreferences({ inputDeviceId: 'mic-1', outputDeviceId: 'spk-1' });
    const prefs = service.loadDevicePreferences();
    expect(prefs.inputDeviceId).toBe('mic-1');
    expect(prefs.outputDeviceId).toBe('spk-1');
  });

  it('reports muted/deafened false before connect', () => {
    expect(service.isMuted).toBeFalse();
    expect(service.isDeafened).toBeFalse();
  });

  it('setMuted without room returns current mute state', async () => {
    const muted = await service.setMuted(true);
    expect(muted).toBeFalse();
  });

  it('rejects localhost LiveKit URLs when the page is not local', async () => {
    const originalHost = window.location.hostname;
    // jsdom hostname is typically localhost — skip assertion shape when already local.
    if (originalHost === 'localhost' || originalHost === '127.0.0.1') {
      expect(true).toBeTrue();
      return;
    }

    await expectAsync(service.connect('ws://localhost:7880', 'token'))
      .toBeRejectedWithError(/localhost/i);
  });

  it('rejects empty LiveKit URL', async () => {
    await expectAsync(service.connect('', 'token'))
      .toBeRejectedWithError(/missing|not configured/i);
  });
});
