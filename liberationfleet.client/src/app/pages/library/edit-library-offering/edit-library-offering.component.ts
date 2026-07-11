import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { LibraryService } from '../../../services/library.service';
import { ToastService } from '../../../components/toast/toast.component';
import { LibraryOfferingListItem } from '../../../models/library.model';

@Component({
  selector: 'app-edit-library-offering',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ConfirmDialogComponent],
  templateUrl: './edit-library-offering.component.html',
  styleUrl: './edit-library-offering.component.css'
})
export class EditLibraryOfferingComponent implements OnInit {
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  deleteButton!: ActionBarButton;
  offering: LibraryOfferingListItem | null = null;
  isOutOfStock = false;
  loading = true;
  saving = false;
  deleting = false;
  errorMessage = '';
  offeringId = 0;
  confirmDeleteVisible = false;

  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.offeringId = Number(this.route.snapshot.paramMap.get('id'));
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things/mine']);
    this.updateActionButtons();

    if (!this.offeringId) {
      this.loading = false;
      this.errorMessage = 'Invalid offering.';
      return;
    }

    this.loadOffering();
  }

  get isStockBased(): boolean {
    return this.offering?.offeringKind === 'Consumable' || this.offering?.offeringKind === 'Service';
  }

  get canToggleOutOfStock(): boolean {
    return !!this.offering?.quantityNotApplicable && this.isStockBased;
  }

  get showTrackedStockNotice(): boolean {
    return this.isStockBased && !this.offering?.quantityNotApplicable;
  }

  toggleOutOfStock() {
    if (!this.canToggleOutOfStock) {
      return;
    }
    this.isOutOfStock = !this.isOutOfStock;
    this.updateActionButtons();
  }

  openDeleteConfirm() {
    this.confirmDeleteVisible = true;
  }

  dismissDeleteConfirm() {
    this.confirmDeleteVisible = false;
  }

  confirmDelete() {
    this.confirmDeleteVisible = false;
    this.deleteOffering();
  }

  private loadOffering() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getMyOfferings({ limit: 100 }).subscribe({
      next: page => {
        const offering = page.items.find(item => item.offeringId === this.offeringId) ?? null;
        if (!offering) {
          this.loading = false;
          this.errorMessage = 'Offering not found.';
          return;
        }

        if (offering.offeringKind === 'Durable') {
          this.loading = false;
          this.errorMessage = 'Durable goods cannot be deleted here. Report them broken or lost from the item page.';
          return;
        }

        this.offering = offering;
        this.isOutOfStock = !!offering.isOutOfStock;
        this.loading = false;
        this.updateActionButtons();
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load offering';
      }
    });
  }

  private save() {
    if (!this.offering || this.saving || !this.canToggleOutOfStock) {
      return;
    }

    this.saving = true;
    this.updateActionButtons();

    this.libraryService.updateOffering(this.offeringId, { isOutOfStock: this.isOutOfStock }).subscribe({
      next: response => {
        this.saving = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update offering');
          this.updateActionButtons();
          return;
        }

        this.toastService.success('Offering updated');
        this.router.navigate(['/app/crew/library-of-things/mine']);
      },
      error: err => {
        this.saving = false;
        this.toastService.error(err?.message ?? 'Failed to update offering');
        this.updateActionButtons();
      }
    });
  }

  private deleteOffering() {
    if (!this.offering || this.deleting || !this.isStockBased) {
      return;
    }

    this.deleting = true;
    this.updateActionButtons();

    this.libraryService.deleteOffering(this.offeringId).subscribe({
      next: response => {
        this.deleting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to delete offering');
          this.updateActionButtons();
          return;
        }

        this.toastService.success('Offering deleted');
        this.router.navigate(['/app/crew/library-of-things/mine']);
      },
      error: err => {
        this.deleting = false;
        this.toastService.error(err?.message ?? 'Failed to delete offering');
        this.updateActionButtons();
      }
    });
  }

  private updateActionButtons() {
    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled: this.saving || this.deleting || !this.canToggleOutOfStock,
      onClick: () => this.save()
    };

    this.deleteButton = {
      label: 'Delete',
      type: 'secondary',
      disabled: this.saving || this.deleting || !this.isStockBased,
      onClick: () => this.openDeleteConfirm()
    };
  }
}
