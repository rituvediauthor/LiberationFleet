import { Component } from '@angular/core';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [NavLayoutComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css'
})
export class ProfileComponent {}
