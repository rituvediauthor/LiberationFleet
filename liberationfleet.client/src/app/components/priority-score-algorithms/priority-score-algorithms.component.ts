import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PriorityScoreBreakdown } from '../../models/profile.model';

@Component({
  selector: 'app-priority-score-algorithms',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './priority-score-algorithms.component.html',
  styleUrl: './priority-score-algorithms.component.css'
})
export class PriorityScoreAlgorithmsComponent {
  @Input() libraryOfThings: PriorityScoreBreakdown | null | undefined;
  @Input() givingSeason: PriorityScoreBreakdown | null | undefined;

  expression(breakdown: PriorityScoreBreakdown): string {
    const crew = this.formatNumber(breakdown.crewLifetimeContributions);
    const emergency = breakdown.emergencyLevel;
    const membership = this.formatNumber(breakdown.membershipBonus);
    const user = this.formatNumber(breakdown.userLifetimeContributions);
    const survival = this.formatNumber(breakdown.survivalThresholdAmount);
    const people = breakdown.peopleRepresentedCount;
    const disability = breakdown.disabilityLevel;
    const factor = this.formatFactor(breakdown.sacrificeBonusFactor);
    return `(${crew}×${emergency} + ${membership} + ${user} + ${survival}) × (${people}+${disability}+1) × ${factor}`;
  }

  labels(breakdown: PriorityScoreBreakdown): string {
    const parts = [
      'Crew contributions × Emergency',
      'Membership',
      'Your contributions',
      'Survival threshold'
    ];
    const multiplier = 'People represented + Disability + 1';
    const boost = breakdown.percentBoost > 0
      ? `Sacrifice boost (+${breakdown.percentBoost}%)`
      : 'Sacrifice boost';
    return `${parts.join(' + ')}\n× ${multiplier}\n× ${boost}`;
  }

  private formatNumber(value: number): string {
    if (Number.isInteger(value)) {
      return String(value);
    }
    return value.toFixed(2).replace(/\.?0+$/, '');
  }

  private formatFactor(value: number): string {
    if (value === 1) {
      return '1';
    }
    return value.toFixed(2).replace(/0+$/, '').replace(/\.$/, '');
  }
}
