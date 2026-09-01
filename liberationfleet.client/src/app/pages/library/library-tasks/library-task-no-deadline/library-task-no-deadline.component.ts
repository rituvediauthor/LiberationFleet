import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { LibraryService } from '../../../../services/library.service';
import { LibraryCryptoService } from '../../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../../services/crew.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { LibraryTaskListItem } from '../../../../models/library.model';

@Component({
  selector: 'app-library-task-no-deadline',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './library-task-no-deadline.component.html',
  styleUrl: './library-task-no-deadline.component.css'
})
export class LibraryTaskNoDeadlineComponent implements OnInit {
  backButton!: ActionBarButton;
  items: LibraryTaskListItem[] = [];
  loading = true;
  errorMessage = '';
  private crewId = 0;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things/tasks']);
  }

  ngOnInit() {
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

  openTask(item: LibraryTaskListItem) {
    this.router.navigate(['/app/crew/library-of-things/tasks', item.taskId]);
  }

  formatValue(value: number): string {
    return Number.isInteger(value) ? value.toString() : value.toFixed(2).replace(/\.?0+$/, '');
  }

  private loadItems() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getNoDeadlineTasks().subscribe({
      next: async items => {
        try {
          const enriched = this.crewId
            ? await this.libraryCrypto.enrichTaskListItems(items, this.crewId)
            : items;
          this.items = [...enriched].sort((a, b) =>
            (a.title || '').localeCompare(b.title || '', undefined, { sensitivity: 'base' })
          );
        } catch {
          this.items = [...items].sort((a, b) =>
            (a.title || '').localeCompare(b.title || '', undefined, { sensitivity: 'base' })
          );
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load tasks';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
