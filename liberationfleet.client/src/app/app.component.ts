import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { ToastContainerComponent } from './components/toast/toast.component';
import { DevToolbarComponent } from './components/dev-toolbar/dev-toolbar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, ToastContainerComponent, DevToolbarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  showDevToolbar = false;
}
