import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { LibraryAccessService } from '../services/library-access.service';

export const libraryAccessGuard: CanActivateFn = () => {
  const libraryAccess = inject(LibraryAccessService);
  const router = inject(Router);

  return libraryAccess.resolveAccess().pipe(
    map(access => {
      if (access.allowed) {
        return true;
      }

      return router.createUrlTree(['/app/crew/library-of-things/unlock'], {
        queryParams: { reason: access.reason }
      });
    })
  );
};
