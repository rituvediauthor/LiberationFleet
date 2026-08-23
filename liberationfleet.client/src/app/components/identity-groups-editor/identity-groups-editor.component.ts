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
    'These groups are more likely to be the target of discrimination and hate crimes, increasing the frequency and severity of hardships they are likely to face. Thus we must prioritize preventing and getting them out of situations of dangerous vulnerability. Select all that apply to you and the people you represent.';
  @Output() selectedChange = new EventEmitter<string[]>();

  readonly options: IdentityGroupOption[] = IDENTITY_GROUP_OPTIONS;

  isSelected(key: string): boolean {
    return this.selected.includes(key);
  }

  onToggle(key: string, checked: boolean): void {
    this.selectedChange.emit(toggleIdentityGroup(this.selected, key, checked));
  }
}
