import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { debounceTime, Subscription } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FriendService } from '../../../services/friend.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { UserSearchResult } from '../../../models/friend.model';
import { CrewmateFriendshipState } from '../../../models/crewmate.model';

@Component({
  selector: 'app-find-friend',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './find-friend.component.html',
  styleUrl: './find-friend.component.css'
})
export class FindFriendComponent implements OnInit, OnDestroy {
  form: FormGroup;
  results: UserSearchResult[] = [];
  searchMessage = '';
  isSearching = false;
  hasSearched = false;
  actionLoading = false;
  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private fb = inject(FormBuilder);
  private friendService = inject(FriendService);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);
  private searchSubscription?: Subscription;

  constructor() {
    this.form = this.fb.group({
      username: ['', [Validators.required, Validators.minLength(2)]]
    });
  }

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/friends']);

    this.primaryButton = {
      label: 'Find friend',
      type: 'primary',
      onClick: () => this.search()
    };

    this.searchSubscription = this.form.get('username')?.valueChanges
      .pipe(debounceTime(300))
      .subscribe(() => this.search());
  }

  ngOnDestroy() {
    this.searchSubscription?.unsubscribe();
  }

  actionLabel(state: CrewmateFriendshipState): string {
    switch (state) {
      case 'requestSent':
        return 'Request sent';
      case 'requestReceived':
        return 'Respond in Requests';
      case 'friends':
        return 'Friends';
      case 'blocked':
        return 'Blocked';
      default:
        return 'Add friend';
    }
  }

  canRequest(state: CrewmateFriendshipState): boolean {
    return state === 'none';
  }

  requestFriend(user: UserSearchResult) {
    if (!this.canRequest(user.friendshipState) || this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.requestFriendship(user.userId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to send request');
          return;
        }
        this.toastService.success(response.message || 'Friend request sent');
        user.friendshipState = response.friendshipState;
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to send request');
      }
    });
  }

  search() {
    const username = (this.form.get('username')?.value as string | null)?.trim() ?? '';
    if (username.length < 2) {
      this.results = [];
      this.searchMessage = '';
      this.hasSearched = false;
      return;
    }

    this.isSearching = true;
    this.hasSearched = true;
    this.searchMessage = 'Searching...';

    this.friendService.searchUsers(username).subscribe({
      next: response => {
        this.isSearching = false;
        if (!response.success) {
          this.searchMessage = response.message || 'Search failed';
          this.results = [];
          return;
        }

        this.results = response.items ?? [];
        this.searchMessage = this.results.length === 0 ? 'No users found.' : '';
      },
      error: () => {
        this.isSearching = false;
        this.searchMessage = 'Search failed';
        this.results = [];
      }
    });
  }
}
