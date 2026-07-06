export type DiscussionKind = 'forums';

export interface DiscussionConfig {
  kind: DiscussionKind;
  label: string;
  labelPlural: string;
  postLabel: string;
  apiPath: string;
  listRoute: string;
  createRoute: string;
  backRoute: string;
  detailRoute: (id: number) => string;
}

export const FORUM_DISCUSSION_CONFIG: DiscussionConfig = {
  kind: 'forums',
  label: 'Forum',
  labelPlural: 'Forums',
  postLabel: 'forum post',
  apiPath: '/api/forums',
  listRoute: '/app/crew/forums',
  createRoute: '/app/crew/forums/create',
  backRoute: '/app/crew',
  detailRoute: id => `/app/crew/forums/${id}`
};

export function getDiscussionConfig(_kind: DiscussionKind): DiscussionConfig {
  return FORUM_DISCUSSION_CONFIG;
}
