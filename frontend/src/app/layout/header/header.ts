import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class HeaderComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected get isBangla(): boolean {
    return document.cookie.includes('googtrans=/en/bn');
  }

  toogleLanguage():void {
    if (this.isBangla) {
      document.cookie = 'googtrans=/en/en; path=/;';
    }
    else {
      document.cookie = 'googtrans=/en/bn; path=/;';
    }
    window.location.reload();
    }
  

  logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/']));
  }
}
