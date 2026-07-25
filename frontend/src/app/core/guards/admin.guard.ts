import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const ready$ = auth.initialized() ? of(null) : auth.bootstrap();

  return ready$.pipe(
    map(() =>
      auth.isAuthenticated() && auth.currentUser()?.isAdmin
        ? true
        : router.createUrlTree(['/']),
    ),
  );
};


