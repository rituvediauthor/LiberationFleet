import {
  formatTypingIndicator,
  TypingActivityController,
  TypingPresenceTracker
} from './typing-indicator.util';

describe('typing-indicator.util', () => {
  beforeEach(() => {
    jasmine.clock().install();
  });

  afterEach(() => {
    jasmine.clock().uninstall();
  });

  it('formats typing labels', () => {
    expect(formatTypingIndicator([])).toBe('');
    expect(formatTypingIndicator(['Alex'])).toBe('Alex is typing...');
    expect(formatTypingIndicator(['Alex', 'Sam'])).toBe('Alex and Sam are typing...');
    expect(formatTypingIndicator(['A', 'B', 'C'])).toBe('Several people are typing...');
  });

  it('sends typing true on input and false after idle', () => {
    const sent: boolean[] = [];
    const controller = new TypingActivityController(isTyping => {
      sent.push(isTyping);
    });

    controller.onInput(true);
    expect(sent).toEqual([true]);

    jasmine.clock().tick(1999);
    expect(sent).toEqual([true]);

    jasmine.clock().tick(1);
    expect(sent).toEqual([true, false]);

    controller.destroy();
  });

  it('resets idle timer on each character', () => {
    const sent: boolean[] = [];
    const controller = new TypingActivityController(isTyping => {
      sent.push(isTyping);
    });

    controller.onInput(true);
    jasmine.clock().tick(1500);
    controller.onInput(true);
    jasmine.clock().tick(1500);
    expect(sent.filter(v => v).length).toBeGreaterThanOrEqual(1);
    expect(sent.includes(false)).toBe(false);

    jasmine.clock().tick(2000);
    expect(sent[sent.length - 1]).toBe(false);

    controller.destroy();
  });

  it('expires remote typers after TTL', () => {
    let label = '';
    const tracker = new TypingPresenceTracker(value => {
      label = value;
    });

    tracker.setTyping('1', 'Alex', true);
    expect(label).toBe('Alex is typing...');

    jasmine.clock().tick(2500);
    expect(label).toBe('');

    tracker.destroy();
  });
});
