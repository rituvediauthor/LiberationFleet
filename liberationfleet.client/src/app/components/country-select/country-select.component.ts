import { Component, EventEmitter, Input, Output } from '@angular/core';
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

  onBlur(): void {
    // Delay so option click registers before close.
    setTimeout(() => {
      this.open = false;
    }, 150);
  }
}
