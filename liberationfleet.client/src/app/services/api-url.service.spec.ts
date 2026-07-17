import { ApiUrlService } from './api-url.service';
import { AppEnvironment } from '../config/app-environment';

describe('ApiUrlService', () => {
  function create(env: AppEnvironment): ApiUrlService {
    return new ApiUrlService(env);
  }

  it('keeps relative paths on web (empty apiBaseUrl)', () => {
    const svc = create({ production: false, apiBaseUrl: '' });
    expect(svc.resolveApi('/api/auth/login')).toBe('/api/auth/login');
    expect(svc.resolveHub('/hubs/chat')).toBe('/hubs/chat');
  });

  it('prefixes absolute API origin for native shells', () => {
    const svc = create({ production: true, apiBaseUrl: 'https://api.example.com/' });
    expect(svc.apiBaseUrl).toBe('https://api.example.com');
    expect(svc.resolveApi('/api/crews')).toBe('https://api.example.com/api/crews');
    expect(svc.resolveHub('/hubs/notifications')).toBe('https://api.example.com/hubs/notifications');
  });
});
