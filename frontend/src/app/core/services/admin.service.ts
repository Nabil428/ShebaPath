
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiBase } from './api-base';

export interface DashboardStats {
  totalUsers: number;
  totalGuides: number;
  totalBlogs: number;
  totalBookmarks: number;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {

  private http = inject(HttpClient);
  private api = inject(ApiBase);

  getDashboard(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(
      this.api.endpoint('admin/dashboard')
    );
  }
}


