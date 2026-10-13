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

type OrderRow = { receptionOrder: number; orderDraft?: string | number | null };

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

  /** Max concurrent active (open) survival thresholds per crewmate. */
  readonly maxActiveThresholdsPerUser = 2;

  get orderValidationMessage(): string {
    return this.describeThresholdAmountProblem()
      ?? this.describeThresholdStackProblem()
      ?? this.describeOrderProblem(this.thresholds, 'Survival threshold')
      ?? this.describeOrderProblem(this.cycles, 'Season cycle')
      ?? '';
  }

  get canSave(): boolean {
    return !this.loading
      && !this.saving
      && !this.errorMessage
      && !this.describeThresholdAmountProblem()
      && !this.describeThresholdStackProblem()
      && this.areOrdersValid(this.thresholds)
      && this.areOrdersValid(this.cycles);
  }

  addThreshold(): void {
    const member = this.members.find(m => this.activeThresholdCountForUser(m.userId) < this.maxActiveThresholdsPerUser)
      ?? null;
    if (!member) {
      this.toastService.error(
        `Every crewmate already has ${this.maxActiveThresholdsPerUser} active survival thresholds.`
      );
      return;
    }
    const nextOrder = this.nextAvailableOrder(this.thresholds);
    this.thresholds = [
      ...this.thresholds,
      {
        clientKey: this.newClientKey(),
        userId: member.userId,
        username: member.username,
        // Remaining tracks need for new open thresholds (0/0 is invalid until need is set).
        amountNeeded: 0,
        amountRemaining: 0,
        receptionOrder: nextOrder,
        orderDraft: String(nextOrder)
      }
    ];
    this.updateSaveButton();
  }

  onThresholdAmountNeededChange(row: ReceptionOrderEditorThreshold): void {
    const needed = Number(row.amountNeeded) || 0;
    // For brand-new rows, keep remaining equal to need so save creates an open threshold.
    if (!row.id) {
      row.amountRemaining = needed;
    } else if ((Number(row.amountRemaining) || 0) > needed) {
      row.amountRemaining = needed;
    }
    this.updateSaveButton();
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
    this.updateSaveButton();
  }

  onThresholdOwnerChange(row: ReceptionOrderEditorThreshold): void {
    const member = this.members.find(m => m.userId === Number(row.userId));
    row.username = member?.username ?? row.username;
    this.updateSaveButton();
  }

  onOrderDraftChange(): void {
    this.updateSaveButton();
  }

  moveThreshold(index: number, delta: number): void {
    this.nudgeOrder(this.thresholds[index], this.thresholds.length, delta);
    this.updateSaveButton();
  }

  canNudgeThreshold(row: ReceptionOrderEditorThreshold, delta: number): boolean {
    return this.canNudgeOrder(row, this.thresholds.length, delta);
  }

  isThresholdOrderInvalid(row: ReceptionOrderEditorThreshold): boolean {
    return this.isOrderDraftInvalid(this.thresholds, row);
  }

  addCycle(): void {
    const member = this.members.find(m => !m.hasPrimaryCycle)
      ?? this.members.find(m => !this.cycles.some(c => c.userId === m.userId && c.isPrimary !== false));
    if (!member) {
      this.toastService.error('Every crewmate already has a primary cycle.');
      return;
    }
    const nextOrder = this.nextAvailableOrder(this.cycles);
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
        receptionOrder: nextOrder,
        orderDraft: String(nextOrder),
        cycleCompleted: false,
        isPrimary: true,
        canRemove: true
      }
    ];
    this.updateSaveButton();
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
    this.updateSaveButton();
  }

  onCycleOwnerChange(row: ReceptionOrderEditorCycle): void {
    const member = this.members.find(m => m.userId === Number(row.userId));
    row.username = member?.username ?? row.username;
  }

  moveCycle(index: number, delta: number): void {
    this.nudgeOrder(this.cycles[index], this.cycles.length, delta);
    this.updateSaveButton();
  }

  canNudgeCycle(row: ReceptionOrderEditorCycle, delta: number): boolean {
    return this.canNudgeOrder(row, this.cycles.length, delta);
  }

  isCycleOrderInvalid(row: ReceptionOrderEditorCycle): boolean {
    return this.isOrderDraftInvalid(this.cycles, row);
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
    this.thresholds = (thresholds ?? [])
      .map(t => ({
        ...t,
        receptionOrder: Number(t.receptionOrder) || 0,
        orderDraft: String(Number(t.receptionOrder) || 0),
        clientKey: this.newClientKey()
      }))
      .sort((a, b) => a.receptionOrder - b.receptionOrder);
    this.cycles = (cycles ?? [])
      .map(c => ({
        ...c,
        receptionOrder: Number(c.receptionOrder) || 0,
        orderDraft: String(Number(c.receptionOrder) || 0),
        clientKey: this.newClientKey(),
        isFrozen: !c.isActive && !c.cycleCompleted
      }))
      .sort((a, b) => a.receptionOrder - b.receptionOrder);
    this.removedThresholdIds = [];
    this.removedCycleIds = [];
    // Initial load only: sync drafts to the server order. After that, drafts are user-owned.
    this.thresholds.forEach((row, i) => {
      row.receptionOrder = i + 1;
      row.orderDraft = String(i + 1);
    });
    this.cycles.forEach((row, i) => {
      row.receptionOrder = i + 1;
      row.orderDraft = String(i + 1);
    });
  }

  private save(): void {
    if (!this.canSave) {
      return;
    }

    const orderedThresholds = this.sortByDrafts(this.thresholds);
    const orderedCycles = this.sortByDrafts(this.cycles);

    this.saving = true;
    this.updateSaveButton();

    this.giftService.saveReceptionOrderEditor({
      thresholds: orderedThresholds.map((t, i) => {
        const amountNeeded = Number(t.amountNeeded) || 0;
        let amountRemaining = Number(t.amountRemaining) || 0;
        // New open thresholds: blank/zero remaining means "still needs the full amount".
        if (!t.id && amountNeeded > 0 && amountRemaining <= 0) {
          amountRemaining = amountNeeded;
        }
        return {
          id: t.id ?? null,
          userId: Number(t.userId),
          username: t.username,
          amountNeeded,
          amountRemaining,
          receptionOrder: i + 1
        };
      }),
      removedThresholdIds: this.removedThresholdIds,
      cycles: orderedCycles.map((c, i) => ({
        id: c.id ?? null,
        userId: Number(c.userId),
        username: c.username,
        amount: Number(c.amount) || 0,
        amountRemaining: Number(c.amountRemaining) || 0,
        isActive: !!c.isActive && !c.cycleCompleted,
        isFrozen: !c.isActive && !c.cycleCompleted,
        receptionOrder: i + 1,
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

  private describeThresholdAmountProblem(): string | null {
    if (this.thresholds.some(t => !t.id && (Number(t.amountNeeded) || 0) <= 0)) {
      return 'New survival thresholds need an amount greater than $0.';
    }
    return null;
  }

  private describeThresholdStackProblem(): string | null {
    const counts = new Map<number, { count: number; name: string }>();
    for (const row of this.thresholds) {
      if (!this.rowWillBeActiveThreshold(row)) {
        continue;
      }
      const userId = Number(row.userId);
      const prev = counts.get(userId);
      if (prev) {
        prev.count += 1;
      } else {
        counts.set(userId, { count: 1, name: row.username || `Crewmate ${userId}` });
      }
    }
    for (const { count, name } of counts.values()) {
      if (count > this.maxActiveThresholdsPerUser) {
        return `${name} can have at most ${this.maxActiveThresholdsPerUser} active survival thresholds.`;
      }
    }
    return null;
  }

  private activeThresholdCountForUser(userId: number): number {
    return this.thresholds.filter(
      t => Number(t.userId) === Number(userId) && this.rowWillBeActiveThreshold(t)
    ).length;
  }

  /** Matches server save: open/unsatisfied after amounts are applied. */
  private rowWillBeActiveThreshold(row: ReceptionOrderEditorThreshold): boolean {
    let needed = Number(row.amountNeeded) || 0;
    let remaining = Number(row.amountRemaining) || 0;
    if (remaining > needed) {
      needed = remaining;
    }
    if (!row.id && remaining <= 0 && needed > 0) {
      remaining = needed;
    }
    if (needed <= 0) {
      return false;
    }
    const received = Math.min(needed, Math.max(0, needed - remaining));
    return received < needed;
  }

  /** True when drafts are exactly the unique set 1..n (empty lists are fine). */
  private areOrdersValid(rows: OrderRow[]): boolean {
    return this.describeOrderProblem(rows, '') === null;
  }

  private describeOrderProblem(rows: OrderRow[], label: string): string | null {
    if (rows.length === 0) {
      return null;
    }

    const values = rows.map(row => this.tryParseDraft(row));
    if (values.some(v => v === null)) {
      return label
        ? `${label} orders must be whole numbers from 1 to ${rows.length}.`
        : 'invalid';
    }

    const nums = values as number[];
    const seen = new Set<number>();
    for (const n of nums) {
      if (n < 1 || n > rows.length) {
        return label
          ? `${label} orders must be whole numbers from 1 to ${rows.length}.`
          : 'invalid';
      }
      if (seen.has(n)) {
        return label
          ? `${label} orders must each be unique (1–${rows.length}).`
          : 'invalid';
      }
      seen.add(n);
    }

    for (let i = 1; i <= rows.length; i++) {
      if (!seen.has(i)) {
        return label
          ? `${label} orders must use each number from 1 to ${rows.length} exactly once.`
          : 'invalid';
      }
    }

    return null;
  }

  private isOrderDraftInvalid(rows: OrderRow[], row: OrderRow): boolean {
    if (this.areOrdersValid(rows)) {
      return false;
    }
    const parsed = this.tryParseDraft(row);
    if (parsed === null || parsed < 1 || parsed > rows.length) {
      return true;
    }
    return rows.filter(r => this.tryParseDraft(r) === parsed).length > 1;
  }

  private tryParseDraft(row: OrderRow): number | null {
    if (row.orderDraft === null || row.orderDraft === undefined || row.orderDraft === '') {
      return null;
    }
    const parsed = typeof row.orderDraft === 'number'
      ? row.orderDraft
      : Number(String(row.orderDraft).trim());
    if (!Number.isFinite(parsed) || !Number.isInteger(parsed)) {
      return null;
    }
    return parsed;
  }

  /** Change only this row's order by ±1 (clamped). Never touches other rows. */
  private nudgeOrder(row: OrderRow | undefined, listLength: number, delta: number): void {
    if (!row || !this.canNudgeOrder(row, listLength, delta)) {
      return;
    }
    const current = this.tryParseDraft(row) ?? row.receptionOrder ?? 1;
    const next = current + delta;
    row.orderDraft = String(next);
    row.receptionOrder = next;
  }

  private canNudgeOrder(row: OrderRow, listLength: number, delta: number): boolean {
    if (listLength <= 0) {
      return false;
    }
    const current = this.tryParseDraft(row);
    if (current === null) {
      return false;
    }
    const next = current + delta;
    return next >= 1 && next <= listLength;
  }

  private nextAvailableOrder(rows: OrderRow[]): number {
    const used = new Set(
      rows
        .map(r => this.tryParseDraft(r))
        .filter((n): n is number => n !== null)
    );
    for (let i = 1; i <= rows.length + 1; i++) {
      if (!used.has(i)) {
        return i;
      }
    }
    return rows.length + 1;
  }

  private sortByDrafts<T extends OrderRow>(rows: T[]): T[] {
    return rows.slice().sort((a, b) => {
      const av = this.tryParseDraft(a) ?? Number.MAX_SAFE_INTEGER;
      const bv = this.tryParseDraft(b) ?? Number.MAX_SAFE_INTEGER;
      return av - bv;
    });
  }

  private newClientKey(): string {
    return `new-${this.nextClientKey++}`;
  }

  private updateSaveButton(): void {
    this.saveButton = {
      label: this.saving ? 'Saving…' : 'Save',
      type: 'primary',
      disabled: !this.canSave,
      onClick: () => this.save()
    };
  }
}
