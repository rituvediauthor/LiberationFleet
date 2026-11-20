import { PullRefreshController } from './pull-refresh.controller';

describe('PullRefreshController', () => {
  it('resets distance via settleOutOfRumbleZone without calling onRefresh', () => {
    const onRefresh = jasmine.createSpy('onRefresh');
    const controller = new PullRefreshController({
      edges: ['top'],
      isBusy: () => false,
      onRefresh
    });
    controller.pullDistance = 30;
    controller.activeEdge = 'top';
    controller.settleOutOfRumbleZone();
    expect(controller.pullDistance).toBe(0);
    expect(controller.activeEdge).toBeNull();
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it('reset clears pull state', () => {
    const controller = new PullRefreshController({
      edges: ['top', 'bottom'],
      isBusy: () => false,
      onRefresh: () => undefined
    });
    controller.pullDistance = 56;
    controller.activeEdge = 'bottom';
    controller.reset();
    expect(controller.pullDistance).toBe(0);
    expect(controller.activeEdge).toBeNull();
  });
});
