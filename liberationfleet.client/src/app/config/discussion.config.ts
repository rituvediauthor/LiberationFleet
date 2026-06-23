export type DiscussionKind = 'forums' | 'projects';

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

export const DISCUSSION_CONFIGS: Record<DiscussionKind, DiscussionConfig> = {
  forums: {
    kind: 'forums',
    label: 'Forum',
    labelPlural: 'Forums',
    postLabel: 'forum post',
    apiPath: '/api/forums',
    listRoute: '/app/crew/forums',
    createRoute: '/app/crew/forums/create',
    backRoute: '/app/crew',
    detailRoute: id => `/app/crew/forums/${id}`
  },
  projects: {
    kind: 'projects',
    label: 'Project',
    labelPlural: 'Projects',
    postLabel: 'project post',
    apiPath: '/api/projects',
    listRoute: '/app/crew/projects',
    createRoute: '/app/crew/projects/create',
    backRoute: '/app/crew',
    detailRoute: id => `/app/crew/projects/${id}`
  }
};

export function getDiscussionConfig(kind: DiscussionKind): DiscussionConfig {
  return DISCUSSION_CONFIGS[kind];
}
