import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { VoiceApiService } from './voice-api.service';

describe('VoiceApiService', () => {
  let service: VoiceApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [VoiceApiService]
    });
    service = TestBed.inject(VoiceApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('joinVoiceRoom posts to voice/join', () => {
    service.joinVoiceRoom(12).subscribe(response => {
      expect(response.success).toBeTrue();
      expect(response.token).toBe('tok');
    });

    const req = httpMock.expectOne('/api/chats/rooms/12/voice/join');
    expect(req.request.method).toBe('POST');
    req.flush({
      success: true,
      message: 'ok',
      token: 'tok',
      wsUrl: 'ws://localhost:7880'
    });
  });

  it('getVoicePresence queries crew snapshot', () => {
    service.getVoicePresence(5).subscribe(response => {
      expect(response.rooms.length).toBe(1);
    });

    const req = httpMock.expectOne(r => r.url === '/api/chats/voice/presence' && r.params.get('crewId') === '5');
    expect(req.request.method).toBe('GET');
    req.flush({
      success: true,
      message: 'ok',
      rooms: [{ chatRoomId: 9, participants: [] }]
    });
  });

  it('serverMuteParticipant posts mute flag', () => {
    service.serverMuteParticipant(3, 44, true).subscribe(response => {
      expect(response.success).toBeTrue();
    });

    const req = httpMock.expectOne('/api/chats/rooms/3/voice/server-mute');
    expect(req.request.body).toEqual({ userId: 44, isServerMuted: true });
    req.flush({ success: true, message: 'muted' });
  });
});
