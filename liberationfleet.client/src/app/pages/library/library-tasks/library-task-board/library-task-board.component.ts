import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { NavigationService } from '../../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { LibraryService } from '../../../../services/library.service';
import { LibraryCryptoService } from '../../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../../services/crew.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { LibraryTaskListItem } from '../../../../models/library.model';

export type QuestBoardTab = 'deadline' | 'no-deadline';

@Component({
  selector: 'app-library-task-board',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './library-task-board.component.html',
  styleUrl: './library-task-board.component.css'
})
export class LibraryTaskBoardComponent implements OnInit {
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  activeTab: QuestBoardTab = 'deadline';
  deadlineItems: LibraryTaskListItem[] = [];
  noDeadlineItems: LibraryTaskListItem[] = [];
  loading = true;
  errorMessage = '';
  private crewId = 0;

  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things']);
    this.createButton = {
      label: 'Create Quest',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/library-of-things/tasks/create'])
    };
  }

  get items(): LibraryTaskListItem[] {
    return this.activeTab === 'deadline' ? this.deadlineItems : this.noDeadlineItems;
  }

  get deadlineCountLabel(): string {
    return this.formatTabCount(this.deadlineItems.length);
  }

  get noDeadlineCountLabel(): string {
    return this.formatTabCount(this.noDeadlineItems.length);
  }

  ngOnInit() {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'no-deadline') {
      this.activeTab = 'no-deadline';
    }

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadItems();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership.';
      }
    });
  }

  selectTab(tab: QuestBoardTab) {
    if (this.activeTab === tab) {
      return;
    }
    this.activeTab = tab;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  openTask(item: LibraryTaskListItem) {
    void this.router.navigate(['/app/crew/library-of-things/tasks', item.taskId]);
  }

  formatValue(value: number): string {
    return Number.isInteger(value) ? value.toString() : value.toFixed(2).replace(/\.?0+$/, '');
  }

  formatDue(item: LibraryTaskListItem): string {
    if (item.scheduleSummary?.trim()) {
      return item.scheduleSummary;
    }
    if (!item.nextDueAt) {
      return '';
    }
    return new Date(item.nextDueAt).toLocaleString();
  }

  formatTabCount(count: number): string {
    return count >= 100 ? '99+' : String(count);
  }

  private loadItems() {
    this.loading = true;
    this.errorMessage = '';

    forkJoin({
      deadline: this.libraryService.getTasks().pipe(catchError(err => {
        this.toastService.error(err?.message ?? 'Failed to load deadline quests');
        return of([] as LibraryTaskListItem[]);
      })),
      noDeadline: this.libraryService.getNoDeadlineTasks().pipe(catchError(err => {
        this.toastService.error(err?.message ?? 'Failed to load no-deadline quests');
        return of([] as LibraryTaskListItem[]);
      }))
    }).subscribe({
      next: async ({ deadline, noDeadline }) => {
        try {
          if (this.crewId) {
            this.deadlineItems = await this.libraryCrypto.enrichTaskListItems(deadline, this.crewId);
            const enrichedNoDeadline = await this.libraryCrypto.enrichTaskListItems(noDeadline, this.crewId);
            this.noDeadlineItems = this.sortAlphabetically(enrichedNoDeadline);
          } else {
            this.deadlineItems = deadline;
            this.noDeadlineItems = this.sortAlphabetically(noDeadline);
          }
        } catch {
          this.deadlineItems = deadline;
          this.noDeadlineItems = this.sortAlphabetically(noDeadline);
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load quests';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private sortAlphabetically(items: LibraryTaskListItem[]): LibraryTaskListItem[] {
    return [...items].sort((a, b) =>
      (a.title || '').localeCompare(b.title || '', undefined, { sensitivity: 'base' })
    );
  }
}
