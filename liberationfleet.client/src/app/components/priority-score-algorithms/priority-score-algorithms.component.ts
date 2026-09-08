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

  /** Scores match for most members; they diverge for organizers (GS last-place vs LoT formula). */
  get scoresMatch(): boolean {
    if (!this.libraryOfThings || !this.givingSeason) {
      return false;
    }
    return this.libraryOfThings.score === this.givingSeason.score
      && this.sameFormulaInputs(this.libraryOfThings, this.givingSeason);
  }

  get singleBreakdown(): PriorityScoreBreakdown | null {
    if (!this.scoresMatch) {
      return null;
    }
    return this.givingSeason ?? this.libraryOfThings ?? null;
  }

  expression(breakdown: PriorityScoreBreakdown): string {
    if (breakdown.score === -1 || breakdown.priorityMultiplier === -1) {
      return '−1 (organizer last-place for concentrated aid cycles)';
    }
    const crew = this.formatNumber(breakdown.crewLifetimeContributions);
    const emergency = breakdown.emergencyLevel;
    const membership = this.formatNumber(breakdown.membershipBonus);
    const user = this.formatNumber(breakdown.userLifetimeContributions);
    const survival = this.formatNumber(breakdown.survivalThresholdAmount);
    const people = breakdown.peopleRepresentedCount;
    const disability = breakdown.disabilityLevel;
    const minorityGroups = breakdown.targetedMinorityGroupCount ?? 0;
    const factor = this.formatFactor(breakdown.sacrificeBonusFactor);
    return `(${crew}×${emergency} + ${membership} + ${user} + ${survival}) × (${people}+${disability}+${minorityGroups}+1) × ${factor}`;
  }

  labels(breakdown: PriorityScoreBreakdown): string {
    if (breakdown.score === -1 || breakdown.priorityMultiplier === -1) {
      return 'Fixed last-place score so the organizer receives concentrated cycle aid after everyone else who is in need';
    }
    const parts = [
      '(Crew contributions × Emergency',
      'Membership',
      'Your contributions',
      'Survival threshold)'
    ];
    const multiplier = '(People represented + Disability + Targeted minority groups + 1');
    const boost = breakdown.percentBoost > 0
      ? `Sacrifice boost (+${breakdown.percentBoost}%)`
      : 'Sacrifice boost';
    return `${parts.join(' + ')}\n× ${multiplier}\n× ${boost}`;
  }

  private sameFormulaInputs(a: PriorityScoreBreakdown, b: PriorityScoreBreakdown): boolean {
    return a.crewLifetimeContributions === b.crewLifetimeContributions
      && a.emergencyLevel === b.emergencyLevel
      && a.membershipBonus === b.membershipBonus
      && a.userLifetimeContributions === b.userLifetimeContributions
      && a.survivalThresholdAmount === b.survivalThresholdAmount
      && a.peopleRepresentedCount === b.peopleRepresentedCount
      && a.disabilityLevel === b.disabilityLevel
      && (a.targetedMinorityGroupCount ?? 0) === (b.targetedMinorityGroupCount ?? 0)
      && a.percentBoost === b.percentBoost;
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
