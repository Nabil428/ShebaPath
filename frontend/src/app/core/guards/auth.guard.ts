import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of, switchMap } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // On a fresh page load / direct URL visit, the "am I logged in?" check
  // (bootstrap) may not have finished yet. Wait for it instead of judging
  // isAuthenticated() before it's had a chance to resolve.
  const ready$ = auth.initialized() ? of(null) : auth.bootstrap();

  return ready$.pipe(
    map(() => (auth.isAuthenticated() ? true : router.createUrlTree(['/login']))),
  );
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const ready$ = auth.initialized() ? of(null) : auth.bootstrap();

  return ready$.pipe(
    map(() => (!auth.isAuthenticated() ? true : router.createUrlTree(['/account']))),
  );
};
