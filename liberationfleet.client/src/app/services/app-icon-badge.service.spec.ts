import { AppIconBadgeService } from './app-icon-badge.service';

describe('AppIconBadgeService', () => {
  it('no-ops on web so Karma never talks to a native plugin', async () => {
    const service = new AppIconBadgeService();
    await expectAsync(service.setCount(4)).toBeResolved();
    await expectAsync(service.setCount(0)).toBeResolved();
  });
});
