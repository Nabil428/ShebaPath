import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-account',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './account.html',
  styleUrl: './account.scss',
})
export class AccountPage {
  private readonly fb = inject(FormBuilder);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly savedMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly saving = signal(false);

  readonly user = computed(() => this.auth.currentUser());

  readonly form = this.fb.nonNullable.group({
    fullName: [this.user()?.fullName ?? '', [Validators.required]],
    phone: [this.user()?.phone ?? ''],
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.savedMessage.set(null);
    this.errorMessage.set(null);
    this.saving.set(true);
    const value = this.form.getRawValue();
    this.auth.updateAccount({ fullName: value.fullName, phone: value.phone || undefined }).subscribe({
      next: () => {
        this.saving.set(false);
        this.savedMessage.set('Your details have been updated.');
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.error ?? 'Could not save your details.');
      },
    });
  }

  logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/']));
  }
}
