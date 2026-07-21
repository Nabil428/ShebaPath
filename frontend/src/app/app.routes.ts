import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/home/home').then((m) => m.HomePage),
    title: 'ShebaPath — Bangladesh Government Service Guides',
  },
  {
    path: 'guides',
    loadComponent: () => import('./pages/guides-list/guides-list').then((m) => m.GuidesListPage),
    title: 'All Guides — ShebaPath',
  },
  {
    path: 'guides/:slug',
    loadComponent: () => import('./pages/guide-detail/guide-detail').then((m) => m.GuideDetailPage),
    title: 'Guide — ShebaPath',
  },
  {
    path: 'blog',
    loadComponent: () => import('./pages/blog-list/blog-list').then((m) => m.BlogListPage),
    title: 'Blog — ShebaPath',
  },
  {
    path: 'blog/:slug',
    loadComponent: () => import('./pages/blog-detail/blog-detail').then((m) => m.BlogDetailPage),
    title: 'Blog — ShebaPath',
  },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login').then((m) => m.LoginPage),
    canActivate: [guestGuard],
    title: 'Log in — ShebaPath',
  },
  {
    path: 'register',
    loadComponent: () => import('./pages/register/register').then((m) => m.RegisterPage),
    canActivate: [guestGuard],
    title: 'Register — ShebaPath',
  },
  {
    path: 'account',
    loadComponent: () => import('./pages/account/account').then((m) => m.AccountPage),
    canActivate: [authGuard],
    title: 'My Account — ShebaPath',
  },

  {
    path: 'privacy',
    loadComponent:() => import('./pages/privacy-policy/privacy-policy').then((m) => m.PrivacyPolicyPage),
    title: 'Privacy Policy - ShebaPath',
  },

  {
    path: '**',
    loadComponent: () => import('./pages/not-found/not-found').then((m) => m.NotFoundPage),
    title: 'Page not found — ShebaPath',
  },


];
