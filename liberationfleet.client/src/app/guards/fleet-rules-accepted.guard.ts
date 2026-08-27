import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { FleetService } from '../services/fleet.service';

/** Require current public fleet rules acceptance before entering most fleet pages. */
export const fleetRulesAcceptedGuard: CanActivateFn = () => {
  const fleetService = inject(FleetService);
  const router = inject(Router);

  return fleetService.getStatus().pipe(
    map(status => {
      if (!status.hasFleet) {
        return router.createUrlTree(['/app/fleet']);
      }
      if (status.needsRuleAcceptance) {
        return router.createUrlTree(['/app/fleet/accept-rules']);
      }
      return true;
    }),
    catchError(() => of(router.createUrlTree(['/app/fleet'])))
  );
};
