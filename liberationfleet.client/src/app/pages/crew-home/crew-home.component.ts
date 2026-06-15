import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { CrewService } from '../../services/crew.service';
import { CrewMembershipStatus } from '../../models/crew.model';

@Component({
  selector: 'app-crew-home',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent],
  templateUrl: './crew-home.component.html',
  styleUrl: './crew-home.component.css'
})
export class CrewHomeComponent implements OnInit {
  membership: CrewMembershipStatus | null = null;

  private router = inject(Router);
  private crewService = inject(CrewService);

  ngOnInit() {
    this.crewService.getMembership().subscribe(status => {
      this.membership = status;
    });
  }

  goToCreateCrew() {
    this.router.navigate(['/app/crew/create']);
  }

  goToJoinCrew() {
    this.router.navigate(['/app/crew/join']);
  }
}
