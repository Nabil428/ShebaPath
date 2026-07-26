import { Routes } from '@angular/router';
import { AdminLayout } from './layout/admin-layout/admin-layout';
import { Dashboard } from './pages/admin/dashboard/dashboard';

import { Guides } from './pages/admin/guides/guides';

import { Blogs } from './pages/admin/blogs/blogs';

import { Categories } from './pages/admin/categories/categories';

import { Tags} from './pages/admin/tags/tags';

import { HeroSlider } from './pages/admin/hero-slider/hero-slider';

import { Settings } from './pages/admin/settings/settings';


import { authGuard, guestGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

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
    path: 'admin/dashboard',
    loadComponent: () =>
      import('./pages/admin/dashboard/dashboard')
        .then((m) => m.Dashboard),
    canActivate: [adminGuard],
    title: 'Admin Dashboard - ShebaPath',
  },

  {
    path: 'admin/guides',
    loadComponent: () =>
      import('./pages/admin/guides/guides')
        .then((m) => m.Guides),
    canActivate: [adminGuard],
    title: 'Manage Guides',
  },

  {
    path: 'admin/guides/new',
    loadComponent: () =>
      import('./pages/admin/admin-guide-form/admin-guide-form').then((m) => m.AdminGuideFormPage),
    canActivate: [adminGuard],
    title: 'New Guide',
  },

  {
    path: 'admin/guides/:slug/edit',
    loadComponent: () =>
      import('./pages/admin/admin-guide-form/admin-guide-form').then((m) => m.AdminGuideFormPage),
    canActivate: [adminGuard],
    title: 'Edit Guide',
  },

  {
    path: 'admin/blogs',
    loadComponent: () =>
      import('./pages/admin/blogs/blogs')
        .then((m) => m.Blogs),
    canActivate: [adminGuard],
    title: 'Manage Blogs',
  },

  {
    path: 'admin/blogs/new',
    loadComponent: () =>
      import('./pages/admin/admin-blog-form/admin-blog-form').then((m) => m.AdminBlogFormPage),
    canActivate: [adminGuard],
    title: 'New Post',
  },

  {
    path: 'admin/blogs/:slug/edit',
    loadComponent: () =>
      import('./pages/admin/admin-blog-form/admin-blog-form').then((m) => m.AdminBlogFormPage),
    canActivate: [adminGuard],
    title: 'Edit Post',
  },

  {
    path: 'admin/categories',
    loadComponent: () =>
      import('./pages/admin/categories/categories')
        .then((m) => m.Categories),
    canActivate: [adminGuard],
    title: 'Categories',
  },

  {
    path: 'admin/tags',
    loadComponent: () =>
      import('./pages/admin/tags/tags')
        .then((m) => m.Tags),
    canActivate: [adminGuard],
    title: 'Tags',
  },

  {
    path: 'admin/hero-slider',
    loadComponent: () =>
      import('./pages/admin/hero-slider/hero-slider')
        .then((m) => m.HeroSlider),
    canActivate: [adminGuard],
    title: 'Hero Slider',
  },

  // IMPORTANT: the wildcard/404 route must always be LAST — it matches
  // every path, so anything placed after it (as it was before) is
  // unreachable and silently falls through to this 404 page instead.
  {
    path: '**',
    loadComponent: () => import('./pages/not-found/not-found').then((m) => m.NotFoundPage),
    title: 'Page not found — ShebaPath',
  },
];
