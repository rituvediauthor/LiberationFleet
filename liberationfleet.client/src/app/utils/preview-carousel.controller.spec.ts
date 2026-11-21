import { PreviewCarouselController } from './preview-carousel.controller';

describe('PreviewCarouselController', () => {
  it('advances on swipe left and goes back on swipe right', () => {
    const onIndexChange = jasmine.createSpy('onIndexChange');
    const onNeedMore = jasmine.createSpy('onNeedMore');
    const carousel = new PreviewCarouselController({
      preferSwipe: () => true,
      onIndexChange,
      onNeedMore
    });
    carousel.setItemCount(4);
    carousel.reset(0);

    carousel.onTouchStart({
      touches: [{ clientX: 200, clientY: 10 }]
    } as unknown as TouchEvent, 300);
    carousel.onTouchMove({
      touches: [{ clientX: 40, clientY: 12 }],
      preventDefault: () => undefined
    } as unknown as TouchEvent);
    carousel.onTouchEnd();

    expect(carousel.activeIndex).toBe(1);
    expect(onIndexChange).toHaveBeenCalledWith(1);

    carousel.onTouchStart({
      touches: [{ clientX: 100, clientY: 10 }]
    } as unknown as TouchEvent, 300);
    carousel.onTouchMove({
      touches: [{ clientX: 220, clientY: 12 }],
      preventDefault: () => undefined
    } as unknown as TouchEvent);
    carousel.onTouchEnd();

    expect(carousel.activeIndex).toBe(0);
  });

  it('requests more when near the end', () => {
    const onNeedMore = jasmine.createSpy('onNeedMore');
    const carousel = new PreviewCarouselController({
      preferSwipe: () => true,
      onIndexChange: () => undefined,
      onNeedMore
    });
    carousel.setItemCount(4);
    carousel.setActiveIndex(2);
    expect(onNeedMore).toHaveBeenCalled();
  });
});
