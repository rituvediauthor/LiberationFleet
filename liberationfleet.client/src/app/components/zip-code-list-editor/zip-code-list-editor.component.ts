import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { isValidPostalCode, normalizePostalCode } from '../../constants/countries';

@Component({
  selector: 'app-zip-code-list-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './zip-code-list-editor.component.html',
  styleUrl: './zip-code-list-editor.component.css'
})
export class ZipCodeListEditorComponent {
  @Input() zipCodes: string[] = [];
  @Input() label = 'Postal codes';
  @Input() hint =
    'People whose profile country and postal code match an entry in this list can find or access this locally.';
  @Input() showError = false;
  @Input() errorText = 'Add at least one valid postal code.';
  @Output() zipCodesChange = new EventEmitter<string[]>();

  draft = '';
  draftError = '';

  addZip(): void {
    const zip = normalizePostalCode(this.draft);
    if (!zip) {
      this.draftError = 'Enter a postal code of 2–12 letters or digits (spaces and hyphens OK).';
      return;
    }

    if (this.zipCodes.includes(zip)) {
      this.draftError = 'That postal code is already listed.';
      return;
    }

    this.draftError = '';
    this.draft = '';
    this.zipCodesChange.emit([...this.zipCodes, zip].sort());
  }

  removeZip(zip: string): void {
    this.zipCodesChange.emit(this.zipCodes.filter(z => z !== zip));
  }

  onDraftKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.addZip();
    }
  }

  /** Expose for templates that still check validity. */
  isValid(value: string): boolean {
    return isValidPostalCode(value);
  }
}
