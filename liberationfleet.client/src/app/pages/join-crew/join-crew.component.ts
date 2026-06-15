import { Component, inject, OnInit } from '@angular/core';
import { debounceTime } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { CrewService } from '../../services/crew.service';
import { ToastService } from '../../components/toast/toast.component';
import { Crew, CrewScope } from '../../models/crew.model';

type JoinMode = 'find' | 'code';

@Component({
  selector: 'app-join-crew',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './join-crew.component.html',
  styleUrl: './join-crew.component.css'
})
export class JoinCrewComponent implements OnInit {
  form: FormGroup;
  backButton: ActionBarButton;
  joinButton: ActionBarButton;

  searchResults: Crew[] = [];
  selectedCrew: Crew | null = null;
  currentPage = 1;
  totalPages = 0;
  totalCount = 0;
  searchMessage = '';
  isSearching = false;
  isJoining = false;
  hasSearched = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
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
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.joinButton = {
      label: 'Join',
      type: 'primary',
      disabled: true,
      onClick: () => this.onJoin()
    };
  }

  ngOnInit() {
    this.form.get('mode')?.valueChanges.subscribe(() => {
      this.resetSearch();
      this.updateLocalValidators();
      this.updateJoinButton();
    });

    this.form.get('scope')?.valueChanges.subscribe(() => {
      this.updateLocalValidators();
      this.resetSearch();
    });

    this.form.valueChanges.pipe(debounceTime(400)).subscribe(() => {
      if (this.isFindMode && this.canSearch()) {
        this.runSearch(1);
      }
      this.updateJoinButton();
    });

    this.updateLocalValidators();
  }

  get isFindMode(): boolean {
    return this.form.get('mode')?.value === 'find';
  }

  get isLocal(): boolean {
    return this.form.get('scope')?.value === 'Local';
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

  private updateJoinButton() {
    if (this.isJoining) {
      this.joinButton.disabled = true;
      return;
    }

    if (this.isFindMode) {
      this.joinButton.disabled = this.selectedCrew === null;
    } else {
      const code = (this.form.get('joinCode')?.value ?? '').trim();
      this.joinButton.disabled = code.length < 4;
    }
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
        this.updateJoinButton();
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Search failed');
        this.isSearching = false;
      }
    });
  }

  selectCrew(crew: Crew) {
    this.selectedCrew = crew;
    this.updateJoinButton();
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

  onJoin() {
    if (this.joinButton.disabled || this.isJoining) {
      return;
    }

    this.isJoining = true;
    this.updateJoinButton();

    const payload = this.isFindMode
      ? { crewId: this.selectedCrew!.id }
      : { joinCode: (this.form.get('joinCode')?.value as string).trim().toUpperCase() };

    this.crewService.join(payload).subscribe({
      next: (result) => {
        if (result.success) {
          this.toastService.success(result.message);
          this.router.navigate(['/app/crew']);
        } else {
          this.toastService.error(result.message);
          this.isJoining = false;
          this.updateJoinButton();
        }
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Failed to join crew');
        this.isJoining = false;
        this.updateJoinButton();
      }
    });
  }
}
