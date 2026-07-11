import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MentionAutocompleteDirective } from '../../directives/mention-autocomplete.directive';

@Component({
  selector: 'app-thread-composer',
  standalone: true,
  imports: [CommonModule, FormsModule, MentionAutocompleteDirective],
  templateUrl: './thread-composer.component.html',
  styleUrl: './thread-composer.component.css'
})
export class ThreadComposerComponent {
  @Input() text = '';
  @Output() textChange = new EventEmitter<string>();

  @Input() mentionedUserIds: number[] = [];
  @Output() mentionedUserIdsChange = new EventEmitter<number[]>();

  @Input() placeholder = 'Write a message...';
  @Input() submitLabel = 'Post';
  @Input() disabled = false;
  @Input() rows = 3;

  @Output() submit = new EventEmitter<void>();

  onTextChange(value: string) {
    this.text = value;
    this.textChange.emit(value);
  }

  onMentionedUserIdsChange(value: number[]) {
    this.mentionedUserIds = value;
    this.mentionedUserIdsChange.emit(value);
  }

  onSubmit() {
    if (this.disabled || !this.text.trim()) {
      return;
    }

    this.submit.emit();
  }
}
