import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiBase } from './api-base';
import { AdminGuidePayload, AdminBlogPayload, Category } from '../models/models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiBase);

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(this.api.endpoint('categories'));
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
