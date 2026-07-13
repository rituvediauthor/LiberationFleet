import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../../services/fleet.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { NavigationService } from '../../../../services/navigation.service';

@Component({
  selector: 'app-fleet-forum-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './fleet-forum-create.component.html',
  styleUrl: './fleet-forum-create.component.css'
})
export class FleetForumCreateComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  isSubmitting = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      body: ['', [Validators.required, Validators.maxLength(10000)]],
      isAdultContent: [false]
    });

    this.backButton = this.navigation.createBackButton(['/app/fleet/forums']);
    this.updateCreateButton();

    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.valueChanges.subscribe(() => this.updateCreateButton());
  }

  onSubmit() {
    if (this.form.invalid || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    const { title, body, isAdultContent } = this.form.getRawValue();
    this.fleetService.createForum({
      title: title.trim(),
      body: body.trim(),
      isAdultContent: !!isAdultContent
    }).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Forum post created');
          if (result.postId) {
            this.router.navigate(['/app/fleet/forums', result.postId]);
          } else {
            this.router.navigate(['/app/fleet/forums']);
          }
          return;
        }
        this.toastService.error(result.message || 'Failed to create forum post');
        this.isSubmitting = false;
        this.updateCreateButton();
      },
      error: err => {
        this.toastService.error(err?.error?.message || 'Failed to create forum post');
        this.isSubmitting = false;
        this.updateCreateButton();
      }
    });
  }

  private updateCreateButton() {
    this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: this.isSubmitting || this.form.invalid,
      onClick: () => this.onSubmit()
    };
  }
}
