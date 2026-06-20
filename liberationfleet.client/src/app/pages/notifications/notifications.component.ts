import { Component } from '@angular/core';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [NavLayoutComponent],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.css'
})
export class NotificationsComponent {}
