import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  IDENTITY_GROUP_OPTIONS,
  IdentityGroupOption,
  toggleIdentityGroup
} from '../../utils/identity-groups.util';

@Component({
  selector: 'app-identity-groups-editor',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './identity-groups-editor.component.html',
  styleUrl: './identity-groups-editor.component.css'
})
export class IdentityGroupsEditorComponent {
  @Input() selected: string[] = [];
  @Input() hint =
    'Select any that apply. Used for mutual-aid priority context within your crew.';
  @Output() selectedChange = new EventEmitter<string[]>();

  readonly options: IdentityGroupOption[] = IDENTITY_GROUP_OPTIONS;

  isSelected(key: string): boolean {
    return this.selected.includes(key);
  }

  onToggle(key: string, checked: boolean): void {
    this.selectedChange.emit(toggleIdentityGroup(this.selected, key, checked));
  }
}
