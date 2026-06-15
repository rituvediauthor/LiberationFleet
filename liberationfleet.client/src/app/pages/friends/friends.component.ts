import { Component } from '@angular/core';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';

@Component({
  selector: 'app-friends',
  standalone: true,
  imports: [NavLayoutComponent],
  templateUrl: './friends.component.html',
  styleUrl: './friends.component.css'
})
export class FriendsComponent {}
