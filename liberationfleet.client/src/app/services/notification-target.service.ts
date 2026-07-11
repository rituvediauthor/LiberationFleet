import { Injectable, inject } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import { FORUM_DISCUSSION_CONFIG } from '../config/discussion.config';
import { ChatService } from './chat.service';
import { CrewDiscussionService } from './crew-discussion.service';
import { CrewmateService } from './crewmate.service';
import { LibraryService } from './library.service';
import { ProposalService } from './proposal.service';
import { RuleService } from './rule.service';

const STATIC_ROUTES = new Set([
  '/app/crew',
  '/app/crew/gift-log',
  '/app/crew/join-season',
  '/app/crew/rules',
  '/app/crew/library-of-things/mine',
  '/app/crew/library-of-things/requests/mine'
]);

@Injectable({
  providedIn: 'root'
})
export class NotificationTargetService {
  private chatService = inject(ChatService);
  private discussionService = inject(CrewDiscussionService);
  private proposalService = inject(ProposalService);
  private libraryService = inject(LibraryService);
  private ruleService = inject(RuleService);
  private crewmateService = inject(CrewmateService);

  isTargetAvailable(actionUrl: string): Observable<boolean> {
    const path = actionUrl.split('?')[0];

    if (STATIC_ROUTES.has(path) || path.startsWith('/app/crew/proposals/list')) {
      return of(true);
    }

    const chatMatch = path.match(/^\/app\/crew\/chats\/(\d+)/);
    if (chatMatch) {
      return this.exists(this.chatService.getRoom(Number(chatMatch[1])));
    }

    const forumMatch = path.match(/^\/app\/crew\/forums\/(\d+)/);
    if (forumMatch) {
      return this.exists(
        this.discussionService.getPost(FORUM_DISCUSSION_CONFIG, Number(forumMatch[1]))
      );
    }

    const proposalMatch = path.match(/^\/app\/crew\/proposals\/(\d+)/);
    if (proposalMatch) {
      return this.exists(this.proposalService.getProposal(Number(proposalMatch[1])));
    }

    const libraryRequestMatch = path.match(/^\/app\/crew\/library-of-things\/requests\/(\d+)/);
    if (libraryRequestMatch) {
      return this.exists(this.libraryService.getRequestDetail(Number(libraryRequestMatch[1])));
    }

    const ruleMatch = path.match(/^\/app\/crew\/rules\/(\d+)\/edit$/);
    if (ruleMatch) {
      return this.exists(this.ruleService.getRule(Number(ruleMatch[1])));
    }

    const crewmateMatch = path.match(/^\/app\/crew\/crewmates\/(\d+)/);
    if (crewmateMatch) {
      return this.exists(this.crewmateService.getCrewmateProfile(Number(crewmateMatch[1])));
    }

    return of(true);
  }

  private exists<T>(request: Observable<T>): Observable<boolean> {
    return request.pipe(
      map(() => true),
      catchError(() => of(false))
    );
  }
}
