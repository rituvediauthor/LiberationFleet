import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../../components/confirm-dialog/confirm-dialog.component';
import { LibraryService } from '../../../../services/library.service';
import { LibraryCryptoService } from '../../../../services/crypto/library-crypto.service';
import { GiftLogCryptoService } from '../../../../services/crypto/gift-log-crypto.service';
import { CrewService } from '../../../../services/crew.service';
import { ToastService } from '../../../../components/toast/toast.component';
import {
  LibraryTaskDetail,
  LibraryTaskInstance,
  LibraryTaskInstanceStatus
} from '../../../../models/library.model';

type SelectionMode = 'Open' | 'Claimed' | 'AwaitingConfirmation' | null;

@Component({
  selector: 'app-library-task-detail',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ConfirmDialogComponent],
  templateUrl: './library-task-detail.component.html',
  styleUrl: './library-task-detail.component.css'
})
export class LibraryTaskDetailComponent implements OnInit {
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;
  secondaryButton: ActionBarButton | null = null;
  task: LibraryTaskDetail | null = null;
  loading = true;
  errorMessage = '';
  actionBusy = false;
  showDeleteConfirm = false;
  selectedIds = new Set<number>();
  selectionMode: SelectionMode = null;
  crewId = 0;
  taskId = 0;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things/tasks']);
  }

  ngOnInit() {
    this.taskId = Number(this.route.snapshot.paramMap.get('id'));
    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadTask();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership.';
      }
    });
  }

  get isNoDeadline(): boolean {
    return !!this.task && !this.task.hasDeadline;
  }

  formatValue(value: number): string {
    return Number.isInteger(value) ? value.toString() : value.toFixed(2).replace(/\.?0+$/, '');
  }

  formatScheduled(value: string): string {
    return new Date(value).toLocaleString();
  }

  formatInstanceWhen(instance: LibraryTaskInstance): string {
    if (this.isNoDeadline) {
      return 'When you can';
    }

    return this.formatScheduled(instance.scheduledAt);
  }

  formatPendingCompletion(instance: LibraryTaskInstance): string {
    const username = instance.claimedByUsername?.trim() || 'Crewmate';
    const when = instance.completedAt
      ? new Date(instance.completedAt).toLocaleString()
      : this.formatScheduled(instance.scheduledAt);
    return `${username} · ${when}`;
  }

  isSelected(instance: LibraryTaskInstance): boolean {
    return this.selectedIds.has(instance.instanceId);
  }

  isClaimedByMe(instance: LibraryTaskInstance): boolean {
    return instance.claimedByCurrentUser && instance.status === 'Claimed';
  }

  isClaimedByOther(instance: LibraryTaskInstance): boolean {
    return instance.status === 'Claimed' && !instance.claimedByCurrentUser;
  }

  canToggle(instance: LibraryTaskInstance): boolean {
    if (this.actionBusy) {
      return false;
    }

    if (!instance.selectable) {
      return false;
    }

    const status = instance.status as LibraryTaskInstanceStatus;

    if (this.isNoDeadline) {
      if (!this.task?.isCreator || status !== 'AwaitingConfirmation') {
        return false;
      }
      return !this.selectionMode || this.selectionMode === 'AwaitingConfirmation';
    }

    if (!this.selectionMode) {
      if (status === 'Open') {
        return true;
      }
      if (status === 'Claimed' && instance.claimedByCurrentUser) {
        return true;
      }
      if (status === 'AwaitingConfirmation' && this.task?.isCreator) {
        return true;
      }
      return false;
    }

    if (this.selectionMode === 'Open') {
      return status === 'Open';
    }
    if (this.selectionMode === 'Claimed') {
      return status === 'Claimed' && instance.claimedByCurrentUser;
    }
    if (this.selectionMode === 'AwaitingConfirmation') {
      return status === 'AwaitingConfirmation' && !!this.task?.isCreator;
    }

    return false;
  }

  toggleInstance(instance: LibraryTaskInstance) {
    const id = instance.instanceId;
    if (this.selectedIds.has(id)) {
      this.selectedIds.delete(id);
      if (this.selectedIds.size === 0) {
        this.selectionMode = null;
      }
      this.refreshActionButtons();
      return;
    }

    if (!this.canToggle(instance)) {
      return;
    }

    if (!this.selectionMode) {
      this.selectionMode = instance.status as SelectionMode;
    }
    this.selectedIds.add(id);
    this.refreshActionButtons();
  }

  private tasksListPath(): string[] {
    return this.isNoDeadline
      ? ['/app/crew/library-of-things/tasks/no-deadline']
      : ['/app/crew/library-of-things/tasks'];
  }

  private refreshActionButtons() {
    if (!this.task) {
      this.primaryButton = null;
      this.secondaryButton = null;
      return;
    }

    if (this.isNoDeadline) {
      this.refreshNoDeadlineButtons();
      return;
    }

    if (this.selectedIds.size === 0) {
      this.primaryButton = this.task.isCreator
        ? {
            label: 'Edit Quest',
            type: 'primary',
            onClick: () =>
              this.router.navigate(['/app/crew/library-of-things/tasks', this.taskId, 'edit'])
          }
        : null;
      this.secondaryButton = this.task.isCreator
        ? {
            label: 'Delete Quest',
            type: 'secondary',
            disabled: this.actionBusy,
            onClick: () => {
              this.showDeleteConfirm = true;
            }
          }
        : null;
      return;
    }

    if (this.selectionMode === 'Open') {
      this.primaryButton = {
        label: 'Claim',
        type: 'primary',
        disabled: this.actionBusy,
        onClick: () => this.runInstanceAction('claim')
      };
      this.secondaryButton = null;
      return;
    }

    if (this.selectionMode === 'Claimed') {
      this.primaryButton = {
        label: 'Complete',
        type: 'primary',
        disabled: this.actionBusy,
        onClick: () => this.runInstanceAction('complete')
      };
      this.secondaryButton = {
        label: 'Unclaim',
        type: 'secondary',
        disabled: this.actionBusy,
        onClick: () => this.runInstanceAction('unclaim')
      };
      return;
    }

    if (this.selectionMode === 'AwaitingConfirmation') {
      this.primaryButton = {
        label: 'Confirm',
        type: 'primary',
        disabled: this.actionBusy,
        onClick: () => this.runInstanceAction('confirm')
      };
      this.secondaryButton = {
        label: 'Mark Incomplete',
        type: 'secondary',
        disabled: this.actionBusy,
        onClick: () => this.runInstanceAction('reject')
      };
      return;
    }

    this.primaryButton = null;
    this.secondaryButton = null;
  }

  private refreshNoDeadlineButtons() {
    if (!this.task) {
      this.primaryButton = null;
      this.secondaryButton = null;
      return;
    }

    if (this.task.isCreator) {
      if (this.selectedIds.size > 0 && this.selectionMode === 'AwaitingConfirmation') {
        this.primaryButton = {
          label: 'Confirm',
          type: 'primary',
          disabled: this.actionBusy,
          onClick: () => this.runInstanceAction('confirm')
        };
        this.secondaryButton = {
          label: 'Incomplete',
          type: 'secondary',
          disabled: this.actionBusy,
          onClick: () => this.runInstanceAction('reject')
        };
        return;
      }

      this.primaryButton = {
        label: 'Edit Quest',
        type: 'primary',
        onClick: () =>
          this.router.navigate(['/app/crew/library-of-things/tasks', this.taskId, 'edit'])
      };
      this.secondaryButton = {
        label: 'Delete Quest',
        type: 'secondary',
        disabled: this.actionBusy,
        onClick: () => {
          this.showDeleteConfirm = true;
        }
      };
      return;
    }

    if (this.task.awaitingConfirmationForCurrentUser) {
      this.primaryButton = null;
      this.secondaryButton = null;
      return;
    }

    if (this.task.canCompleteAnytime) {
      this.primaryButton = {
        label: 'Complete',
        type: 'primary',
        disabled: this.actionBusy,
        onClick: () => this.runNoDeadlineComplete()
      };
      this.secondaryButton = null;
      return;
    }

    this.primaryButton = null;
    this.secondaryButton = null;
  }

  private runNoDeadlineComplete() {
    if (!this.task || this.actionBusy) {
      return;
    }

    this.actionBusy = true;
    this.refreshActionButtons();
    this.libraryService.completeNoDeadlineTask(this.taskId).subscribe({
      next: response => {
        if (!response.success) {
          this.actionBusy = false;
          this.refreshActionButtons();
          this.toastService.error(response.message || 'Action failed');
          return;
        }
        this.onActionSuccess(response.message);
      },
      error: (err: unknown) => this.onActionError(err)
    });
  }

  private runInstanceAction(action: 'claim' | 'unclaim' | 'complete' | 'confirm' | 'reject') {
    if (!this.task || this.selectedIds.size === 0 || this.actionBusy) {
      return;
    }

    const instanceIds = Array.from(this.selectedIds);
    this.actionBusy = true;
    this.refreshActionButtons();

    if (action === 'confirm') {
      this.libraryService.confirmTaskInstances(this.taskId, instanceIds).subscribe({
        next: async response => {
          if (!response.success) {
            this.actionBusy = false;
            this.refreshActionButtons();
            this.toastService.error(response.message || 'Action failed');
            return;
          }

          for (const gift of response.contributionGifts ?? []) {
            try {
              await this.giftLogCrypto.encryptLibraryTaskCompletion(
                {
                  ...gift,
                  itemTitle: this.task?.title || gift.itemTitle
                },
                this.crewId
              );
            } catch {
              // Gift already recorded server-side; encryption is best-effort.
            }
          }

          if (response.taskClosed) {
            this.toastService.success(response.message || 'Completion confirmed.');
            this.selectedIds.clear();
            this.selectionMode = null;
            this.actionBusy = false;
            void this.router.navigate(this.tasksListPath());
            return;
          }

          this.onActionSuccess(response.message);
        },
        error: (err: unknown) => this.onActionError(err)
      });
      return;
    }

    const request$ =
      action === 'claim'
        ? this.libraryService.claimTaskInstances(this.taskId, instanceIds)
        : action === 'unclaim'
          ? this.libraryService.unclaimTaskInstances(this.taskId, instanceIds)
          : action === 'complete'
            ? this.libraryService.completeTaskInstances(this.taskId, instanceIds)
            : this.libraryService.rejectTaskCompletion(this.taskId, instanceIds);

    request$.subscribe({
      next: response => {
        if (!response.success) {
          this.actionBusy = false;
          this.refreshActionButtons();
          this.toastService.error(response.message || 'Action failed');
          return;
        }
        this.onActionSuccess(response.message);
      },
      error: (err: unknown) => this.onActionError(err)
    });
  }

  private onActionSuccess(message?: string) {
    this.toastService.success(message || 'Updated.');
    this.selectedIds.clear();
    this.selectionMode = null;
    this.actionBusy = false;
    this.loadTask();
  }

  private onActionError(err: unknown) {
    this.actionBusy = false;
    this.refreshActionButtons();
    const message =
      err && typeof err === 'object' && 'error' in err
        ? (err as { error?: { message?: string }; message?: string }).error?.message
          ?? (err as { message?: string }).message
        : err && typeof err === 'object' && 'message' in err
          ? String((err as { message?: string }).message)
          : undefined;
    this.toastService.error(message || 'Action failed');
  }

  private loadTask() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getTask(this.taskId).subscribe({
      next: async task => {
        try {
          this.task = this.crewId
            ? await this.libraryCrypto.enrichTaskDetail(task, this.crewId)
            : task;
        } catch {
          this.task = task;
        }
        this.loading = false;
        this.backButton = this.navigation.createBackButton(this.tasksListPath());
        this.pruneSelection();
        this.refreshActionButtons();
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load quest';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  onDeleteConfirm() {
    this.showDeleteConfirm = false;
    if (!this.task?.isCreator || this.actionBusy) {
      return;
    }

    this.actionBusy = true;
    this.refreshActionButtons();
    this.libraryService.deleteTask(this.taskId).subscribe({
      next: response => {
        this.actionBusy = false;
        if (!response.success) {
          this.refreshActionButtons();
          this.toastService.error(response.message || 'Failed to delete quest');
          return;
        }
        this.toastService.success(response.message || 'Quest deleted.');
        void this.router.navigate(this.tasksListPath());
      },
      error: (err: unknown) => this.onActionError(err)
    });
  }

  onDeleteCancel() {
    this.showDeleteConfirm = false;
  }

  private pruneSelection() {
    if (!this.task) {
      this.selectedIds.clear();
      this.selectionMode = null;
      return;
    }

    const validIds = new Set(this.task.instances.map(i => i.instanceId));
    for (const id of Array.from(this.selectedIds)) {
      if (!validIds.has(id)) {
        this.selectedIds.delete(id);
      }
    }
    if (this.selectedIds.size === 0) {
      this.selectionMode = null;
    }
  }
}
