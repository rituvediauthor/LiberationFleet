import { Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LibraryCategory } from '../../models/library.model';

@Component({
  selector: 'app-library-category-picker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './library-category-picker.component.html',
  styleUrl: './library-category-picker.component.css'
})
export class LibraryCategoryPickerComponent {
  @Input() categories: LibraryCategory[] = [];
  @Input() selectedIds: number[] = [];
  @Input() searchPlaceholder = 'Search categories...';
  @Input() emptyMessage = 'No matching categories';
  @Output() selectedIdsChange = new EventEmitter<number[]>();

  @ViewChild('dropdownRoot') dropdownRoot?: ElementRef<HTMLElement>;

  searchQuery = '';
  dropdownOpen = false;

  get selectedSet(): Set<number> {
    return new Set(this.selectedIds);
  }

  get selectedCategories(): LibraryCategory[] {
    const selected = this.selectedSet;
    return this.categories.filter(category => selected.has(category.id));
  }

  get availableCategories(): LibraryCategory[] {
    const selected = this.selectedSet;
    const query = this.searchQuery.trim().toLowerCase();
    return this.categories
      .filter(category => !selected.has(category.id))
      .filter(category => !query || category.name.toLowerCase().includes(query));
  }

  openDropdown() {
    this.dropdownOpen = true;
  }

  onSearchInput() {
    this.dropdownOpen = true;
  }

  selectCategory(categoryId: number) {
    if (this.selectedSet.has(categoryId)) {
      return;
    }

    this.emitSelection([...this.selectedIds, categoryId]);
    this.searchQuery = '';
    this.dropdownOpen = this.availableCategories.length > 0;
  }

  removeCategory(categoryId: number) {
    this.emitSelection(this.selectedIds.filter(id => id !== categoryId));
    this.dropdownOpen = true;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    const root = this.dropdownRoot?.nativeElement;
    if (root && !root.contains(event.target as Node)) {
      this.dropdownOpen = false;
    }
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape' && this.dropdownOpen) {
      event.preventDefault();
      this.dropdownOpen = false;
    }
  }

  private emitSelection(ids: number[]) {
    this.selectedIds = ids;
    this.selectedIdsChange.emit(ids);
  }
}
