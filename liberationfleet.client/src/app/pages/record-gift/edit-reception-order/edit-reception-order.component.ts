import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { GiftService } from '../../../services/gift.service';
import { ToastService } from '../../../components/toast/toast.component';
import {
  ReceptionOrderEditorCycle,
  ReceptionOrderEditorMember,
  ReceptionOrderEditorThreshold
} from '../../../models/gift.model';

@Component({
  selector: 'app-edit-reception-order',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent],
  templateUrl: './edit-reception-order.component.html',
  styleUrl: './edit-reception-order.component.css'
})
export class EditReceptionOrderComponent implements OnInit {
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  loading = true;
  saving = false;
  errorMessage = '';
  members: ReceptionOrderEditorMember[] = [];
  thresholds: ReceptionOrderEditorThreshold[] = [];
  cycles: ReceptionOrderEditorCycle[] = [];
  removedThresholdIds: number[] = [];
  removedCycleIds: number[] = [];

  private navigation = inject(NavigationService);
  private giftService = inject(GiftService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private nextClientKey = 1;

  ngOnInit(): void {
    this.backButton = this.navigation.createBackButton(['/app/crew/gift-log/record']);
    this.updateSaveButton();
    this.loadEditor();
  }

  addThreshold(): void {
    const member = this.members[0];
    if (!member) {
      this.toastService.error('No crewmates available.');
      return;
    }
    this.thresholds = [
      ...this.thresholds,
      {
        clientKey: this.newClientKey(),
        userId: member.userId,
        username: member.username,
        amountNeeded: 0,
        amountRemaining: 0,
        order: this.thresholds.length + 1
      }
    ];
    this.renumberThresholdOrders();
  }

  removeThreshold(index: number): void {
    const row = this.thresholds[index];
    if (!row) {
      return;
    }
    if (row.id) {
      this.removedThresholdIds = [...this.removedThresholdIds, row.id];
    }
    this.thresholds = this.thresholds.filter((_, i) => i !== index);
    this.renumberThresholdOrders();
  }

  onThresholdOwnerChange(row: ReceptionOrderEditorThreshold): void {
    const member = this.members.find(m => m.userId === Number(row.userId));
    row.username = member?.username ?? row.username;
  }

  addCycle(): void {
    const member = this.members.find(m => !m.hasPrimaryCycle)
      ?? this.members.find(m => !this.cycles.some(c => c.userId === m.userId && c.isPrimary !== false));
    if (!member) {
      this.toastService.error('Every crewmate already has a primary cycle.');
      return;
    }
    this.cycles = [
      ...this.cycles,
      {
        clientKey: this.newClientKey(),
        userId: member.userId,
        username: member.username,
        amount: 0,
        amountRemaining: 0,
        isActive: false,
        isFrozen: true,
        order: this.cycles.length + 1,
        cycleCompleted: false,
        isPrimary: true,
        canRemove: true
      }
    ];
    this.renumberCycleOrders();
  }

  removeCycle(index: number): void {
    const row = this.cycles[index];
    if (!row) {
      return;
    }
    if (row.isEmergencyCycle || row.isPaybackCycle) {
      this.toastService.error('Emergency/payback cycles cannot be removed here.');
      return;
    }
    if (row.id && row.canRemove === false) {
      this.toastService.error('This cycle has linked gifts. Mark it completed instead.');
      return;
    }
    if (row.id) {
      this.removedCycleIds = [...this.removedCycleIds, row.id];
    }
    this.cycles = this.cycles.filter((_, i) => i !== index);
    this.renumberCycleOrders();
  }

  onCycleOwnerChange(row: ReceptionOrderEditorCycle): void {
    const member = this.members.find(m => m.userId === Number(row.userId));
    row.username = member?.username ?? row.username;
  }

  onCycleActiveChange(row: ReceptionOrderEditorCycle, active: boolean): void {
    row.isActive = active;
    row.isFrozen = !active && !row.cycleCompleted;
    if (active) {
      row.cycleCompleted = false;
    }
  }

  onCycleCompletedChange(row: ReceptionOrderEditorCycle, completed: boolean): void {
    row.cycleCompleted = completed;
    if (completed) {
      row.isActive = false;
      row.isFrozen = false;
      if (row.amount > 0) {
        row.amountRemaining = 0;
      }
    } else {
      row.isFrozen = !row.isActive;
    }
  }

  trackThreshold(_index: number, row: ReceptionOrderEditorThreshold): string {
    return row.clientKey ?? `t-${row.id ?? _index}`;
  }

  trackCycle(_index: number, row: ReceptionOrderEditorCycle): string {
    return row.clientKey ?? `c-${row.id ?? _index}`;
  }

  private loadEditor(): void {
    this.loading = true;
    this.errorMessage = '';
    this.giftService.getReceptionOrderEditor().subscribe({
      next: response => {
        this.loading = false;
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load reception order editor.';
          this.toastService.error(this.errorMessage);
          this.updateSaveButton();
          return;
        }
        this.applyEditor(response.members, response.thresholds, response.cycles);
        this.updateSaveButton();
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.message || 'Failed to load reception order editor.';
        this.toastService.error(this.errorMessage);
        this.updateSaveButton();
      }
    });
  }

  private applyEditor(
    members: ReceptionOrderEditorMember[],
    thresholds: ReceptionOrderEditorThreshold[],
    cycles: ReceptionOrderEditorCycle[]
  ): void {
    this.members = members ?? [];
    this.thresholds = (thresholds ?? []).map(t => ({
      ...t,
      clientKey: this.newClientKey()
    }));
    this.cycles = (cycles ?? []).map(c => ({
      ...c,
      clientKey: this.newClientKey(),
      isFrozen: !c.isActive && !c.cycleCompleted
    }));
    this.removedThresholdIds = [];
    this.removedCycleIds = [];
    this.renumberThresholdOrders();
    this.renumberCycleOrders();
  }

  private save(): void {
    if (this.saving || this.loading) {
      return;
    }
    this.saving = true;
    this.updateSaveButton();

    const sortedThresholds = [...this.thresholds].sort(
      (a, b) => (Number(a.order) || 0) - (Number(b.order) || 0)
    );
    const sortedCycles = [...this.cycles].sort(
      (a, b) => (Number(a.order) || 0) - (Number(b.order) || 0)
    );

    this.giftService.saveReceptionOrderEditor({
      thresholds: sortedThresholds.map((t, i) => ({
        id: t.id ?? null,
        userId: Number(t.userId),
        username: t.username,
        amountNeeded: Number(t.amountNeeded) || 0,
        amountRemaining: Number(t.amountRemaining) || 0,
        order: i + 1
      })),
      removedThresholdIds: this.removedThresholdIds,
      cycles: sortedCycles.map((c, i) => ({
        id: c.id ?? null,
        userId: Number(c.userId),
        username: c.username,
        amount: Number(c.amount) || 0,
        amountRemaining: Number(c.amountRemaining) || 0,
        isActive: !!c.isActive && !c.cycleCompleted,
        isFrozen: !c.isActive && !c.cycleCompleted,
        order: i + 1,
        cycleCompleted: !!c.cycleCompleted,
        isEmergencyCycle: !!c.isEmergencyCycle,
        isPaybackCycle: !!c.isPaybackCycle,
        isPrimary: c.isPrimary !== false,
        canRemove: c.canRemove !== false
      })),
      removedCycleIds: this.removedCycleIds
    }).subscribe({
      next: result => {
        this.saving = false;
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to save reception order.');
          this.updateSaveButton();
          return;
        }
        this.toastService.success(result.message || 'Reception order saved.');
        if (result.editor?.success) {
          this.applyEditor(result.editor.members, result.editor.thresholds, result.editor.cycles);
        }
        this.updateSaveButton();
        void this.router.navigate(['/app/crew/gift-log/record']);
      },
      error: err => {
        this.saving = false;
        this.toastService.error(err?.error?.message || 'Failed to save reception order.');
        this.updateSaveButton();
      }
    });
  }

  private renumberThresholdOrders(): void {
    this.thresholds.forEach((row, i) => {
      row.order = i + 1;
    });
  }

  private renumberCycleOrders(): void {
    this.cycles.forEach((row, i) => {
      row.order = i + 1;
    });
  }

  private newClientKey(): string {
    return `new-${this.nextClientKey++}`;
  }

  private updateSaveButton(): void {
    this.saveButton = {
      label: this.saving ? 'Saving…' : 'Save',
      type: 'primary',
      disabled: this.loading || this.saving || !!this.errorMessage,
      onClick: () => this.save()
    };
  }
}
