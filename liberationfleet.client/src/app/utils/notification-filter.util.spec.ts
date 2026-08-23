import { NotificationCategoryMapperMatches } from './notification-filter.util';

describe('NotificationCategoryMapperMatches', () => {
  it('treats legacy proposal NewReply as Proposals, not Comments', () => {
    expect(
      NotificationCategoryMapperMatches(
        'NewReply',
        'Proposals',
        '/app/crew/proposals/7?commentId=3'
      )
    ).toBe(true);

    expect(
      NotificationCategoryMapperMatches(
        'NewReply',
        'Comments',
        '/app/crew/proposals/7?commentId=3'
      )
    ).toBe(false);
  });

  it('keeps forum NewReply under Comments', () => {
    expect(
      NotificationCategoryMapperMatches(
        'NewReply',
        'Comments',
        '/app/crew/forums/8?commentId=2'
      )
    ).toBe(true);
  });
});
