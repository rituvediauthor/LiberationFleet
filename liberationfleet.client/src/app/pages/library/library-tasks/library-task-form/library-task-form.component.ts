import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { CharCounterComponent } from '../../../../components/char-counter/char-counter.component';
import { LibraryService } from '../../../../services/library.service';
import { LibraryCryptoService } from '../../../../services/crypto/library-crypto.service';
import { CryptoSessionService } from '../../../../services/crypto/crypto-session.service';
import { CrewService } from '../../../../services/crew.service';
import { ToastService } from '../../../../components/toast/toast.component';
import {
  LibraryTaskDetail,
  LibraryTaskFrequency,
  UpsertLibraryTaskRequest
} from '../../../../models/library.model';
import { isControlInvalidForA11y } from '../../../../utils/a11y-form.util';
import { TextFieldLimits } from '../../../../utils/text-field-limits';

@Component({
  selector: 'app-library-task-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, CharCounterComponent],
  templateUrl: './library-task-form.component.html',
  styleUrl: './library-task-form.component.css'
})
export class LibraryTaskFormComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  loading = false;
  submitting = false;
  isEdit = false;
  taskId: number | null = null;
  private crewId = 0;
  readonly titleMaxLength = TextFieldLimits.title;
  readonly detailsMaxLength = TextFieldLimits.longBody;
  readonly weekDayLabels = [
    { value: 0, label: 'Sun' },
    { value: 1, label: 'Mon' },
    { value: 2, label: 'Tue' },
    { value: 3, label: 'Wed' },
    { value: 4, label: 'Thu' },
    { value: 5, label: 'Fri' },
    { value: 6, label: 'Sat' }
  ];
  readonly monthDayOptions = Array.from({ length: 31 }, (_, i) => i + 1);
  readonly monthOptions = [
    { value: 1, label: 'January' },
    { value: 2, label: 'February' },
    { value: 3, label: 'March' },
    { value: 4, label: 'April' },
    { value: 5, label: 'May' },
    { value: 6, label: 'June' },
    { value: 7, label: 'July' },
    { value: 8, label: 'August' },
    { value: 9, label: 'September' },
    { value: 10, label: 'October' },
    { value: 11, label: 'November' },
    { value: 12, label: 'December' }
  ];
  readonly frequencies: { value: LibraryTaskFrequency; label: string }[] = [
    { value: 'Daily', label: 'Daily' },
    { value: 'Weekly', label: 'Weekly' },
    { value: 'Monthly', label: 'Monthly' },
    { value: 'Yearly', label: 'Yearly' }
  ];

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private cryptoSession = inject(CryptoSessionService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(this.titleMaxLength)]],
      details: ['', [Validators.maxLength(this.detailsMaxLength)]],
      value: [1, [Validators.required, Validators.min(0.01)]],
      hasDeadline: [false],
      deleteOnCompletion: [false],
      isRecurring: [false],
      frequency: ['Daily' as LibraryTaskFrequency],
      timeSpecific: [false],
      timeHour: [12],
      timeMinute: [0],
      timePeriod: ['am'],
      isSpaced: [false],
      interval: [1, [Validators.min(1)]],
      daySpecific: [false],
      weekDays: [[] as number[]],
      monthDays: [[] as number[]],
      yearMonth: [1],
      yearDay: [1],
      oneShotDate: ['']
    });

    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things/tasks']);
    this.saveButton = {
      label: 'Create',
      type: 'primary',
      onClick: () => this.submit()
    };
  }

  ngOnInit() {
    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        const idParam = this.route.snapshot.paramMap.get('id');
        if (idParam) {
          this.isEdit = true;
          this.taskId = Number(idParam);
          this.saveButton = {
            label: 'Save',
            type: 'primary',
            onClick: () => this.submit()
          };
          this.backButton = this.navigation.createBackButton([
            '/app/crew/library-of-things/tasks',
            String(this.taskId)
          ]);
          this.loadTask(this.taskId);
        }
      },
      error: () => this.toastService.error('Failed to load crew membership.')
    });
  }

  get hasDeadline(): boolean {
    return !!this.form.get('hasDeadline')?.value;
  }

  get isRecurring(): boolean {
    return !!this.form.get('isRecurring')?.value;
  }

  get frequency(): LibraryTaskFrequency {
    return (this.form.get('frequency')?.value as LibraryTaskFrequency) || 'Daily';
  }

  get timeSpecific(): boolean {
    return !!this.form.get('timeSpecific')?.value;
  }

  get isSpaced(): boolean {
    return !!this.form.get('isSpaced')?.value;
  }

  get daySpecific(): boolean {
    return !!this.form.get('daySpecific')?.value;
  }

  get spacedUnit(): string {
    switch (this.frequency) {
      case 'Weekly':
        return 'weeks';
      case 'Monthly':
        return 'months';
      case 'Yearly':
        return 'years';
      default:
        return 'days';
    }
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  isWeekDaySelected(day: number): boolean {
    return (this.form.get('weekDays')?.value as number[] | null)?.includes(day) ?? false;
  }

  toggleWeekDay(day: number) {
    const current = [...((this.form.get('weekDays')?.value as number[]) ?? [])];
    const index = current.indexOf(day);
    if (index >= 0) {
      current.splice(index, 1);
    } else {
      current.push(day);
    }
    current.sort((a, b) => a - b);
    this.form.patchValue({ weekDays: current });
  }

  isMonthDaySelected(day: number): boolean {
    return (this.form.get('monthDays')?.value as number[] | null)?.includes(day) ?? false;
  }

  toggleMonthDay(day: number) {
    const current = [...((this.form.get('monthDays')?.value as number[]) ?? [])];
    const index = current.indexOf(day);
    if (index >= 0) {
      current.splice(index, 1);
    } else {
      current.push(day);
    }
    current.sort((a, b) => a - b);
    this.form.patchValue({ monthDays: current });
  }

  async submit() {
    if (this.submitting) {
      return;
    }

    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.toastService.error('Please fix the highlighted fields.');
      return;
    }

    if (!this.crewId) {
      this.toastService.error('Crew is required to save a task.');
      return;
    }

    if (!this.cryptoSession.isUnlocked()) {
      this.toastService.error('Unlock encryption to create or edit tasks.');
      return;
    }

    const basePayload = this.buildPayload();
    if (!basePayload) {
      return;
    }

    this.submitting = true;
    this.saveButton = { ...this.saveButton, disabled: true };

    try {
      const encrypted = await this.libraryCrypto.encryptTaskPayload(this.crewId, {
        title: basePayload.title,
        details: basePayload.details
      });
      const payload: UpsertLibraryTaskRequest = {
        ...basePayload,
        title: '',
        details: '',
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext,
        keyVersion: encrypted.keyVersion
      };

      const response = this.isEdit && this.taskId
        ? await new Promise((resolve, reject) =>
            this.libraryService.updateTask(this.taskId!, payload).subscribe({ next: resolve, error: reject }))
        : await new Promise((resolve, reject) =>
            this.libraryService.createTask(payload).subscribe({ next: resolve, error: reject }));

      this.submitting = false;
      this.saveButton = { ...this.saveButton, disabled: false };
      const result = response as { success: boolean; message?: string; taskId?: number };
      if (!result.success) {
        this.toastService.error(result.message || 'Failed to save task');
        return;
      }

      this.toastService.success(this.isEdit ? 'Task updated.' : 'Task created.');
      const nextId = result.taskId ?? this.taskId;
      if (nextId) {
        void this.router.navigate(['/app/crew/library-of-things/tasks', nextId]);
      } else if (!basePayload.hasDeadline) {
        void this.router.navigate(['/app/crew/library-of-things/tasks/no-deadline']);
      } else {
        void this.router.navigate(['/app/crew/library-of-things/tasks']);
      }
    } catch (err: unknown) {
      this.submitting = false;
      this.saveButton = { ...this.saveButton, disabled: false };
      const message = (err as { error?: { message?: string }; message?: string })?.error?.message
        ?? (err as { message?: string })?.message
        ?? 'Failed to save task';
      this.toastService.error(message);
    }
  }

  private loadTask(taskId: number) {
    this.loading = true;
    this.libraryService.getTask(taskId).subscribe({
      next: async task => {
        try {
          const enriched = this.crewId
            ? await this.libraryCrypto.enrichTaskDetail(task, this.crewId)
            : task;
          this.patchFromTask(enriched);
        } catch {
          this.patchFromTask(task);
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toastService.error(err?.message ?? 'Failed to load task');
        void this.router.navigate(['/app/crew/library-of-things/tasks']);
      }
    });
  }

  private patchFromTask(task: LibraryTaskDetail) {
    const timeParts = this.minutesToParts(task.specificTimeMinutes ?? 0);
    let oneShotDate = '';
    if (task.oneShotDueAt) {
      const d = new Date(task.oneShotDueAt);
      oneShotDate = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
      if (task.timeSpecific) {
        const parts = this.minutesToParts(d.getHours() * 60 + d.getMinutes());
        timeParts.hour = parts.hour;
        timeParts.minute = parts.minute;
        timeParts.period = parts.period;
      }
    }

    this.form.patchValue({
      title: task.title,
      details: task.details ?? '',
      value: task.value,
      hasDeadline: !!task.hasDeadline,
      deleteOnCompletion: !!task.deleteOnCompletion,
      isRecurring: task.isRecurring,
      frequency: task.isRecurring ? (task.frequency as LibraryTaskFrequency) || 'Daily' : 'Daily',
      timeSpecific: task.timeSpecific,
      timeHour: timeParts.hour,
      timeMinute: timeParts.minute,
      timePeriod: timeParts.period,
      isSpaced: task.isSpaced,
      interval: task.interval || 1,
      daySpecific: task.daySpecific,
      weekDays: [...(task.weekDays ?? [])],
      monthDays: [...(task.monthDays ?? [])],
      yearMonth: task.yearMonth ?? 1,
      yearDay: task.yearDay ?? 1,
      oneShotDate
    });
  }

  private buildPayload(): UpsertLibraryTaskRequest | null {
    const raw = this.form.getRawValue();
    const hasDeadline = !!raw.hasDeadline;
    const deleteOnCompletion = !hasDeadline && !!raw.deleteOnCompletion;
    const isRecurring = hasDeadline && !!raw.isRecurring;
    const frequency: LibraryTaskFrequency = isRecurring
      ? (raw.frequency as LibraryTaskFrequency) || 'Daily'
      : 'None';
    const timeSpecific = hasDeadline && !!raw.timeSpecific;
    const specificTimeMinutes = timeSpecific
      ? this.partsToMinutes(Number(raw.timeHour), Number(raw.timeMinute), raw.timePeriod)
      : null;

    let oneShotDueAt: string | null = null;
    if (hasDeadline && !isRecurring) {
      const dateStr = (raw.oneShotDate as string)?.trim();
      if (!dateStr) {
        this.toastService.error('Due date is required when Deadline is checked.');
        return null;
      }
      const [y, m, d] = dateStr.split('-').map(Number);
      const due = new Date(y, m - 1, d);
      if (timeSpecific && specificTimeMinutes != null) {
        due.setHours(Math.floor(specificTimeMinutes / 60), specificTimeMinutes % 60, 0, 0);
      } else {
        due.setHours(0, 0, 0, 0);
      }
      oneShotDueAt = due.toISOString();
    }

    if (isRecurring && frequency === 'Weekly' && raw.daySpecific && !(raw.weekDays as number[]).length) {
      this.toastService.error('Select at least one weekday.');
      return null;
    }
    if (isRecurring && frequency === 'Monthly' && raw.daySpecific && !(raw.monthDays as number[]).length) {
      this.toastService.error('Select at least one day of the month.');
      return null;
    }

    return {
      title: (raw.title as string).trim(),
      details: ((raw.details as string) ?? '').trim(),
      value: Number(raw.value),
      hasDeadline,
      deleteOnCompletion,
      isRecurring,
      frequency,
      timeSpecific,
      specificTimeMinutes,
      isSpaced: hasDeadline && !!raw.isSpaced,
      interval: Math.max(1, Number(raw.interval) || 1),
      daySpecific: hasDeadline && !!raw.daySpecific,
      weekDays: isRecurring && frequency === 'Weekly' && raw.daySpecific
        ? [...((raw.weekDays as number[]) ?? [])]
        : [],
      monthDays: isRecurring && frequency === 'Monthly' && raw.daySpecific
        ? [...((raw.monthDays as number[]) ?? [])]
        : [],
      yearMonth: isRecurring && frequency === 'Yearly' ? Number(raw.yearMonth) || 1 : null,
      yearDay: isRecurring && frequency === 'Yearly' ? Number(raw.yearDay) || 1 : null,
      oneShotDueAt,
      nonce: '',
      ciphertext: '',
      keyVersion: 1
    };
  }

  private minutesToParts(total: number): { hour: number; minute: number; period: 'am' | 'pm' } {
    const clamped = Math.max(0, Math.min(23 * 60 + 59, total));
    const h24 = Math.floor(clamped / 60);
    const minute = clamped % 60;
    const period: 'am' | 'pm' = h24 >= 12 ? 'pm' : 'am';
    let hour = h24 % 12;
    if (hour === 0) {
      hour = 12;
    }
    return { hour, minute, period };
  }

  private partsToMinutes(hour: number, minute: number, period: string): number {
    let h = Math.max(1, Math.min(12, hour || 12));
    const m = Math.max(0, Math.min(59, minute || 0));
    if (period === 'am') {
      h = h === 12 ? 0 : h;
    } else {
      h = h === 12 ? 12 : h + 12;
    }
    return h * 60 + m;
  }
}
