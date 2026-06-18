import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { CrewService } from '../../services/crew.service';
import { GiftService } from '../../services/gift.service';
import { CrewMembershipStatus } from '../../models/crew.model';
import { NextAidInfo } from '../../models/gift.model';

@Component({
  selector: 'app-crew-home',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent],
  templateUrl: './crew-home.component.html',
  styleUrl: './crew-home.component.css'
})
export class CrewHomeComponent implements OnInit {
  membership: CrewMembershipStatus | null = null;
  nextAid: NextAidInfo | null = null;

  private router = inject(Router);
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);

  ngOnInit() {
    this.crewService.getMembership().subscribe(status => {
      this.membership = status;
      if (status.hasCrew) {
        this.giftService.getNextAidInfo().subscribe({
          next: info => this.nextAid = info
        });
      }
    });
  }

  goToCreateCrew() {
    this.router.navigate(['/app/crew/create']);
  }

  goToJoinCrew() {
    this.router.navigate(['/app/crew/join']);
  }

  goToEditCrew() {
    this.router.navigate(['/app/crew/edit']);
  }

  goToGiftLog() {
    this.giftService.navigateToGiftLogEntry(this.router);
  }

  goToChats() {
    this.router.navigate(['/app/crew/chats']);
  }

  goToProposals() {
    this.router.navigate(['/app/crew/proposals']);
  }

  goToProjects() {
    this.router.navigate(['/app/crew/projects']);
  }
}
