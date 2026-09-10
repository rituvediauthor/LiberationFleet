import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { COUNTRY_OPTIONS, CountryOption } from '../../constants/countries';

@Component({
  selector: 'app-country-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './country-select.component.html',
  styleUrl: './country-select.component.css'
})
export class CountrySelectComponent {
  @Input() countryCode: string | null = null;
  @Input() label = 'Country';
  @Input() hint = '';
  @Input() required = false;
  @Input() showError = false;
  @Input() errorText = 'Select a country.';
  @Input() allowClear = true;
  @Output() countryCodeChange = new EventEmitter<string | null>();

  @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;

  query = '';
  open = false;

  readonly options = COUNTRY_OPTIONS;

  get selectedLabel(): string {
    if (!this.countryCode) {
      return '';
    }
    const match = this.options.find(o => o.code === this.countryCode);
    return match ? `${match.name} (${match.code})` : this.countryCode;
  }

  get filtered(): CountryOption[] {
    const q = this.query.trim().toLowerCase();
    if (!q) {
      return this.options;
    }
    return this.options.filter(
      o => o.name.toLowerCase().includes(q) || o.code.toLowerCase().includes(q)
    );
  }

  toggleOpen(): void {
    this.open = !this.open;
    if (this.open) {
      this.query = '';
      this.focusSearch();
    }
  }

  select(option: CountryOption): void {
    this.countryCodeChange.emit(option.code);
    this.open = false;
    this.query = '';
  }

  clear(event: Event): void {
    event.stopPropagation();
    this.countryCodeChange.emit(null);
    this.open = false;
    this.query = '';
  }

  onControlFocusOut(event: FocusEvent): void {
    const host = event.currentTarget as HTMLElement | null;
    const next = event.relatedTarget as Node | null;
    if (host && next && host.contains(next)) {
      return;
    }

    // Option buttons use mousedown; relatedTarget can be null briefly.
    setTimeout(() => {
      if (!host?.contains(document.activeElement)) {
        this.open = false;
      }
    }, 0);
  }

  onTriggerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && this.open) {
      event.preventDefault();
      this.open = false;
      return;
    }

    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.toggleOpen();
    }
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.open = false;
    }
  }

  private focusSearch(): void {
    setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
  }
}
