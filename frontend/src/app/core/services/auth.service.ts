import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, Observable, of, tap, throwError } from 'rxjs';
import { ApiBase } from './api-base';
import { AppUser, LoginPayload, RegisterPayload, UpdateAccountPayload } from '../models/models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiBase);

  private readonly currentUserSignal = signal<AppUser | null>(null);
  private readonly initializedSignal = signal(false);
  private readonly loadingSignal = signal(false);

  readonly currentUser = computed(() => this.currentUserSignal());
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null);
  readonly initialized = computed(() => this.initializedSignal());
  readonly loading = computed(() => this.loadingSignal());

  bootstrap(): Observable<AppUser | null> {
    return this.http.get<AppUser>(this.api.endpoint('auth/me'), { withCredentials: true }).pipe(
      tap((user) => this.currentUserSignal.set(user)),
      catchError(() => {
        this.currentUserSignal.set(null);
        return of(null);
      }),
      finalize(() => this.initializedSignal.set(true)),
    );
  }

  register(payload: RegisterPayload): Observable<AppUser> {
    this.loadingSignal.set(true);
    return this.http.post<AppUser>(this.api.endpoint('auth/register'), payload, { withCredentials: true }).pipe(
      tap((user) => this.currentUserSignal.set(user)),
      catchError((err: HttpErrorResponse) => throwError(() => err)),
      finalize(() => this.loadingSignal.set(false)),
    );
  }

  login(payload: LoginPayload): Observable<AppUser> {
    this.loadingSignal.set(true);
    return this.http.post<AppUser>(this.api.endpoint('auth/login'), payload, { withCredentials: true }).pipe(
      tap((user) => this.currentUserSignal.set(user)),
      catchError((err: HttpErrorResponse) => throwError(() => err)),
      finalize(() => this.loadingSignal.set(false)),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(this.api.endpoint('auth/logout'), {}, { withCredentials: true }).pipe(
      tap(() => this.currentUserSignal.set(null)),
    );
  }

  forgotPassword(email: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(this.api.endpoint('auth/forgot-password'), { email });
  }

  resetPassword(token: string, newPassword: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(this.api.endpoint('auth/reset-password'), { token, newPassword });
  }

  deleteAccount(): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(this.api.endpoint('account'), { withCredentials: true }).pipe(
      tap(() => this.currentUserSignal.set(null)),
    );
  }

 

  updateAccount(payload: UpdateAccountPayload): Observable<AppUser> {
    this.loadingSignal.set(true);
    return this.http.patch<AppUser>(this.api.endpoint('account'), payload, { withCredentials: true }).pipe(
      tap((user) => this.currentUserSignal.set(user)),
      catchError((err: HttpErrorResponse) => throwError(() => err)),
      finalize(() => this.loadingSignal.set(false)),
    );
  }
}
