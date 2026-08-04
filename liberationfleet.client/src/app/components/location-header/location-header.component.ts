import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocationHeaderInfo } from '../../utils/location-header.util';

@Component({
  selector: 'app-location-header',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './location-header.component.html',
  styleUrl: './location-header.component.css'
})
export class LocationHeaderComponent {
  @Input({ required: true }) info!: LocationHeaderInfo;
}
