import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiBase } from './api-base';
import { AdminGuidePayload, AdminBlogPayload, Category, DashboardStats, Tag, HeroSlide } from '../models/models';

// Re-exported so files that import DashboardStats/Category directly from this
// service (instead of from models.ts) still resolve correctly.
export type { Category, DashboardStats };

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiBase);

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(this.api.endpoint('categories'));
  }

  getDashboard(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(this.api.endpoint('admin/dashboard'), {
      withCredentials: true,
    });
  }

  // ----- Categories -----
  createCategory(payload: { name: string; description?: string }): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.api.endpoint('admin/categories'), payload, { withCredentials: true });
  }
  updateCategory(id: number, payload: { name: string; description?: string }): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(this.api.endpoint(`admin/categories/${id}`), payload, { withCredentials: true });
  }
  deleteCategory(id: number): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(this.api.endpoint(`admin/categories/${id}`), { withCredentials: true });
  }

  // ----- Tags -----
  getTags(): Observable<Tag[]> {
    return this.http.get<Tag[]>(this.api.endpoint('admin/tags'), { withCredentials: true });
  }
  createTag(name: string): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.api.endpoint('admin/tags'), { name }, { withCredentials: true });
  }
  updateTag(id: number, name: string): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(this.api.endpoint(`admin/tags/${id}`), { name }, { withCredentials: true });
  }
  deleteTag(id: number): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(this.api.endpoint(`admin/tags/${id}`), { withCredentials: true });
  }

  // ----- Hero slides -----
  getHeroSlides(): Observable<HeroSlide[]> {
    return this.http.get<HeroSlide[]>(this.api.endpoint('admin/hero-slides'), { withCredentials: true });
  }
  createHeroSlide(payload: Omit<HeroSlide, 'id'>): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.api.endpoint('admin/hero-slides'), payload, { withCredentials: true });
  }
  updateHeroSlide(id: number, payload: Omit<HeroSlide, 'id'>): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(this.api.endpoint(`admin/hero-slides/${id}`), payload, { withCredentials: true });
  }
  deleteHeroSlide(id: number): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(this.api.endpoint(`admin/hero-slides/${id}`), { withCredentials: true });
  }

  // ----- Image upload -----
  uploadImage(file: File): Observable<{ imageUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ imageUrl: string }>(this.api.endpoint('admin/upload'), formData, { withCredentials: true });
  }

  createGuide(payload: AdminGuidePayload): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(this.api.endpoint('admin/guides'), payload, {
      withCredentials: true,
    });
  }

  updateGuide(slug: string, payload: AdminGuidePayload): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(this.api.endpoint(`admin/guides/${slug}`), payload, {
      withCredentials: true,
    });
  }

  deleteGuide(slug: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(this.api.endpoint(`admin/guides/${slug}`), {
      withCredentials: true,
    });
  }

  createBlogPost(payload: AdminBlogPayload): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(this.api.endpoint('admin/blog'), payload, {
      withCredentials: true,
    });
  }

  updateBlogPost(slug: string, payload: AdminBlogPayload): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(this.api.endpoint(`admin/blog/${slug}`), payload, {
      withCredentials: true,
    });
  }

  deleteBlogPost(slug: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(this.api.endpoint(`admin/blog/${slug}`), {
      withCredentials: true,
    });
  }
}
