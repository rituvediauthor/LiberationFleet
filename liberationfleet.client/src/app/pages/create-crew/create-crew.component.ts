import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { HubLoadingComponent } from '../../components/hub-loading/hub-loading.component';
import { CountrySelectComponent } from '../../components/country-select/country-select.component';
import { ZipCodeListEditorComponent } from '../../components/zip-code-list-editor/zip-code-list-editor.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { CrewService } from '../../services/crew.service';
import { NavigationService } from '../../services/navigation.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewPrivacy, CrewScope } from '../../models/crew.model';
import { isControlInvalidForA11y } from '../../utils/a11y-form.util';
import { TextFieldLimits } from '../../utils/text-field-limits';
import { CharCounterComponent } from '../../components/char-counter/char-counter.component';

@Component({
  selector: 'app-create-crew',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageLayoutComponent,
    HubLoadingComponent,
    CharCounterComponent,
    CountrySelectComponent,
    ZipCodeListEditorComponent,
    ConfirmDialogComponent
  ],
  templateUrl: './create-crew.component.html',
  styleUrl: './create-crew.component.css'
})
export class CreateCrewComponent implements OnInit, OnDestroy {
  form: FormGroup;
  backButton: ActionBarButton;
  createButton: ActionBarButton;
  isLoading = false;
  hasCrew = false;
  showLeaveConfirm = false;
  readonly nameMaxLength = TextFieldLimits.orgName;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private subscription = new Subscription();

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(this.nameMaxLength)]],
      maxSize: [30, [Validators.required, Validators.min(2), Validators.max(50)]],
      privacy: ['Public' as CrewPrivacy, Validators.required],
      scope: ['Online' as CrewScope, Validators.required],
      countryCode: [null as string | null],
      allowedZipCodes: [[] as string[]]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew']);

    this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: true,
      onClick: () => this.onCreateClicked()
    };

    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.get('scope')?.valueChanges.subscribe(() => this.updateLocalValidators());
    this.updateLocalValidators();
  }

  ngOnInit() {
    this.subscription.add(
      this.crewService.getMembership().pipe(catchError(() => of(null))).subscribe(status => {
        this.hasCrew = !!status?.hasCrew;
        this.backButton = this.navigation.createBackButton(
          this.hasCrew ? ['/app/crew/find'] : ['/app/crew']
        );
      })
    );
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  get isLocal(): boolean {
    return this.form.get('scope')?.value === 'Local';
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  onCountryCodeChange(code: string | null) {
    this.form.patchValue({ countryCode: code });
    this.form.get('countryCode')?.markAsTouched();
    this.updateCreateButton();
  }

  onAllowedZipCodesChange(zips: string[]) {
    this.form.patchValue({ allowedZipCodes: zips });
    this.form.get('allowedZipCodes')?.markAsTouched();
    this.updateCreateButton();
  }

  private updateLocalValidators() {
    const country = this.form.get('countryCode');
    const zips = this.form.get('allowedZipCodes');

    if (this.isLocal) {
      country?.setValidators([Validators.required]);
      zips?.setValidators([Validators.minLength(1)]);
    } else {
      country?.clearValidators();
      country?.setValue(null, { emitEvent: false });
      zips?.clearValidators();
      zips?.setValue([] as string[], { emitEvent: false });
    }

    country?.updateValueAndValidity({ emitEvent: false });
    zips?.updateValueAndValidity({ emitEvent: false });
    this.updateCreateButton();
  }

  private updateCreateButton() {
    this.createButton.disabled = !this.form.valid || this.isLoading;
  }

  onCreateClicked() {
    if (this.form.invalid || this.isLoading) {
      return;
    }

    if (this.hasCrew) {
      this.showLeaveConfirm = true;
      return;
    }

    this.submitCreate();
  }

  onLeaveConfirm() {
    this.showLeaveConfirm = false;
    this.submitCreate();
  }

  onLeaveCancel() {
    this.showLeaveConfirm = false;
  }

  onSubmit() {
    this.onCreateClicked();
  }

  private submitCreate() {
    if (this.form.invalid || this.isLoading) {
      return;
    }

    this.isLoading = true;
    this.updateCreateButton();

    const scope = this.form.get('scope')?.value as CrewScope;
    const isLocal = scope === 'Local';
    const payload = {
      name: this.form.get('name')?.value,
      maxSize: Number(this.form.get('maxSize')?.value),
      privacy: this.form.get('privacy')?.value as CrewPrivacy,
      scope,
      countryCode: isLocal ? (this.form.get('countryCode')?.value as string) : null,
      allowedZipCodes: isLocal ? [...(this.form.get('allowedZipCodes')?.value ?? [])] : []
    };

    this.crewService.create(payload).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message);
          void this.router.navigate(['/app/crew']);
        } else {
          this.toastService.error(result.message);
          this.isLoading = false;
          this.updateCreateButton();
        }
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to create crew');
        this.isLoading = false;
        this.updateCreateButton();
      }
    });
  }
}
