import { Component, inject, OnInit } from '@angular/core';
import { debounceTime } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { CrewService } from '../../services/crew.service';
import { NavigationService } from '../../services/navigation.service';
import { ToastService } from '../../components/toast/toast.component';
import { Crew, CrewScope, PublicCrewRule } from '../../models/crew.model';

type JoinMode = 'find' | 'code';
type JoinStep = 'select' | 'rules';
const JOIN_CODE_LENGTH = 8;

@Component({
  selector: 'app-join-crew',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, RouterLink],
  templateUrl: './join-crew.component.html',
  styleUrl: './join-crew.component.css'
})
export class JoinCrewComponent implements OnInit {
  readonly joinCodeLength = JOIN_CODE_LENGTH;

  form: FormGroup;
  backButton: ActionBarButton;
  primaryButton!: ActionBarButton;

  joinStep: JoinStep = 'select';
  searchResults: Crew[] = [];
  selectedCrew: Crew | null = null;
  currentPage = 1;
  totalPages = 0;
  totalCount = 0;
  searchMessage = '';
  isSearching = false;
  isLoadingRules = false;
  isSubmitting = false;
  hasSearched = false;

  targetCrewId = 0;
  targetCrewName = '';
  publicRules: PublicCrewRule[] = [];
  acceptedRuleIds = new Set<number>();
  rulesError = '';

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      mode: ['find' as JoinMode, Validators.required],
      joinCode: [''],
      scope: ['Online' as CrewScope, Validators.required],
      zipCode: [''],
      radiusMiles: [25]
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.onBack()
    };
  }

  ngOnInit() {
    this.updatePrimaryButton();

    this.form.get('mode')?.valueChanges.subscribe(() => {
      this.resetSearch();
      this.updateLocalValidators();
      this.updatePrimaryButton();
      this.refreshSearchIfNeeded();
    });

    this.form.get('scope')?.valueChanges.subscribe(() => {
      this.updateLocalValidators();
      this.resetSearch();
      this.refreshSearchIfNeeded();
    });

    this.form.valueChanges.pipe(debounceTime(400)).subscribe(() => {
      if (this.isFindMode && this.canSearch()) {
        this.runSearch(1);
      }
      this.updatePrimaryButton();
    });

    this.updateLocalValidators();
    this.refreshSearchIfNeeded();
  }

  get isFindMode(): boolean {
    return this.form.get('mode')?.value === 'find';
  }

  get isLocal(): boolean {
    return this.form.get('scope')?.value === 'Local';
  }

  get allRulesAccepted(): boolean {
    return this.publicRules.every(rule => this.acceptedRuleIds.has(rule.id));
  }

  isRuleAccepted(ruleId: number): boolean {
    return this.acceptedRuleIds.has(ruleId);
  }

  toggleRuleAcceptance(ruleId: number, accepted: boolean) {
    if (accepted) {
      this.acceptedRuleIds.add(ruleId);
    } else {
      this.acceptedRuleIds.delete(ruleId);
    }
    this.updatePrimaryButton();
  }

  onJoinCodeInput(event: Event) {
    const input = event.target as HTMLInputElement;
    const normalized = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, JOIN_CODE_LENGTH);
    if (input.value !== normalized) {
      this.form.patchValue({ joinCode: normalized }, { emitEvent: true });
    }
  }

  private onBack() {
    if (this.joinStep === 'rules') {
      this.joinStep = 'select';
      this.publicRules = [];
      this.acceptedRuleIds.clear();
      this.rulesError = '';
      this.updatePrimaryButton();
      return;
    }
    this.navigation.back(['/app/crew']);
  }

  private updateLocalValidators() {
    if (!this.isFindMode) {
      return;
    }

    const zip = this.form.get('zipCode');
    const radius = this.form.get('radiusMiles');

    if (this.isLocal) {
      zip?.setValidators([Validators.required, Validators.pattern(/^\d{5}$/)]);
      radius?.setValidators([Validators.required, Validators.min(1), Validators.max(500)]);
    } else {
      zip?.clearValidators();
      radius?.clearValidators();
    }

    zip?.updateValueAndValidity({ emitEvent: false });
    radius?.updateValueAndValidity({ emitEvent: false });
  }

  private updatePrimaryButton() {
    if (this.joinStep === 'rules') {
      const disabled = this.isSubmitting || this.isLoadingRules || !this.allRulesAccepted;
      this.primaryButton = {
        label: 'Request to join',
        type: 'primary',
        disabled,
        onClick: () => this.onSubmitJoinRequest()
      };
      return;
    }

    const disabled = this.isLoadingRules || (this.isFindMode
      ? this.selectedCrew === null
      : this.normalizeJoinCode(this.form.get('joinCode')?.value).length !== JOIN_CODE_LENGTH);

    this.primaryButton = {
      label: 'Continue',
      type: 'primary',
      disabled,
      onClick: () => this.onContinueToRules()
    };
  }

  private normalizeJoinCode(value: unknown): string {
    return String(value ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, JOIN_CODE_LENGTH);
  }

  canSearch(): boolean {
    if (!this.isFindMode) {
      return false;
    }

    if (this.isLocal) {
      const zipValid = /^\d{5}$/.test(this.form.get('zipCode')?.value ?? '');
      const radius = Number(this.form.get('radiusMiles')?.value);
      return zipValid && radius >= 1 && radius <= 500;
    }

    return true;
  }

  private refreshSearchIfNeeded() {
    if (this.canSearch()) {
      this.runSearch(1);
    }
  }

  runSearch(page: number) {
    if (!this.canSearch() || this.isSearching) {
      return;
    }

    this.isSearching = true;
    this.currentPage = page;
    const scope = this.form.get('scope')?.value as CrewScope;

    this.crewService.search({
      scope,
      zipCode: scope === 'Local' ? String(this.form.get('zipCode')?.value).padStart(5, '0') : undefined,
      radiusMiles: scope === 'Local' ? Number(this.form.get('radiusMiles')?.value) : undefined,
      page,
      pageSize: 10
    }).subscribe({
      next: (result) => {
        this.hasSearched = true;
        this.searchResults = result.items;
        this.totalPages = result.totalPages;
        this.totalCount = result.totalCount;
        this.searchMessage = result.message;
        this.selectedCrew = null;
        this.isSearching = false;
        this.updatePrimaryButton();
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Search failed');
        this.isSearching = false;
      }
    });
  }

  selectCrew(crew: Crew) {
    this.selectedCrew = crew;
    this.updatePrimaryButton();
  }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }
    this.runSearch(page);
  }

  resetSearch() {
    this.searchResults = [];
    this.selectedCrew = null;
    this.currentPage = 1;
    this.totalPages = 0;
    this.totalCount = 0;
    this.searchMessage = '';
    this.hasSearched = false;
  }

  onContinueToRules() {
    if (this.primaryButton.disabled || this.isLoadingRules) {
      return;
    }

    this.isLoadingRules = true;
    this.rulesError = '';
    this.updatePrimaryButton();

    const request = this.isFindMode
      ? this.crewService.getPublicRules(this.selectedCrew!.id)
      : this.crewService.getPublicRulesByJoinCode(this.normalizeJoinCode(this.form.get('joinCode')?.value));

    request.subscribe({
      next: (result) => {
        this.isLoadingRules = false;
        if (!result.success) {
          this.toastService.error(result.message);
          this.updatePrimaryButton();
          return;
        }

        this.targetCrewId = result.crewId;
        this.targetCrewName = result.crewName;
        this.publicRules = result.items;
        this.acceptedRuleIds.clear();
        this.joinStep = 'rules';
        this.rulesError = '';
        this.updatePrimaryButton();
      },
      error: (error) => {
        this.isLoadingRules = false;
        this.toastService.error(this.extractErrorMessage(error));
        this.updatePrimaryButton();
      }
    });
  }

  onSubmitJoinRequest() {
    if (this.primaryButton.disabled || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updatePrimaryButton();

    const payload = this.isFindMode
      ? {
          crewId: this.targetCrewId,
          acceptedRuleIds: this.publicRules.map(rule => rule.id)
        }
      : {
          joinCode: this.normalizeJoinCode(this.form.get('joinCode')?.value),
          acceptedRuleIds: this.publicRules.map(rule => rule.id)
        };

    this.crewService.submitJoinRequest(payload).subscribe({
      next: (result) => {
        if (result.success) {
          this.toastService.success(result.message || 'Join request submitted');
          this.router.navigate(['/app/crew/join-requests']);
          return;
        }
        this.toastService.error(result.message);
        this.isSubmitting = false;
        this.updatePrimaryButton();
      },
      error: (error) => {
        this.toastService.error(this.extractErrorMessage(error));
        this.isSubmitting = false;
        this.updatePrimaryButton();
      }
    });
  }

  private extractErrorMessage(error: { error?: { message?: string; errors?: Record<string, string[]> } }): string {
    const validationErrors = error.error?.errors;
    if (validationErrors) {
      const firstError = Object.values(validationErrors).flat()[0];
      if (firstError) {
        return firstError;
      }
    }

    return error.error?.message || 'Request failed';
  }
}
