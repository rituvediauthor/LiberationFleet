import { UserActivityItem } from '../models/activity.model';

export function getActivityRoute(item: UserActivityItem): string[] | null {
  if (!item.isAccessible) {
    return null;
  }

  switch (item.kind) {
    case 'ChatRoom':
      return item.chatRoomType === 'Voice'
        ? ['/app/crew/chats', String(item.resourceId), 'voice']
        : ['/app/crew/chats', String(item.resourceId)];
    case 'ChatMessage':
      if (!item.parentResourceId) {
        return null;
      }
      return item.chatRoomType === 'Voice'
        ? ['/app/crew/chats', String(item.parentResourceId), 'voice']
        : ['/app/crew/chats', String(item.parentResourceId)];
    case 'ForumPost':
      return ['/app/crew/forums', String(item.resourceId)];
    case 'ForumComment':
      return item.parentResourceId
        ? ['/app/crew/forums', String(item.parentResourceId)]
        : null;
    case 'LibraryOffering':
      return item.libraryUnitId
        ? ['/app/crew/library-of-things/units', String(item.libraryUnitId)]
        : ['/app/crew/library-of-things/mine'];
    case 'LibraryRequest':
      return ['/app/crew/library-of-things/requests', String(item.resourceId)];
    case 'LibraryRequestMessage':
      return item.parentResourceId
        ? ['/app/crew/library-of-things/requests', String(item.parentResourceId), 'chat']
        : null;
    case 'LibraryMaintenance':
      return item.libraryUnitId
        ? ['/app/crew/library-of-things/units', String(item.libraryUnitId)]
        : null;
    case 'Gift':
      return item.relatedUserId
        ? ['/app/profile/gift-history', String(item.relatedUserId)]
        : ['/app/crew/gift-log'];
    case 'Proposal':
      return ['/app/crew/proposals', String(item.resourceId)];
    case 'ProposalComment':
      return item.parentResourceId
        ? ['/app/crew/proposals', String(item.parentResourceId)]
        : null;
    default:
      return null;
  }
}
