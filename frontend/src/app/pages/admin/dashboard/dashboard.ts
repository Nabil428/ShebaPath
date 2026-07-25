import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminService, DashboardStats } from '../../../core/services/admin.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {

  private adminService = inject(AdminService);

  stats: DashboardStats = {
    totalUsers: 0,
    totalGuides: 0,
    totalBlogs: 0,
    totalBookmarks: 0
  };
loading: any;

  ngOnInit() {
    this.loadDashboard();
  }

  loadDashboard() {

  this.loading = true;

  this.adminService.getDashboard().subscribe({

    next: (res) => {

      this.stats = res;

      this.loading = false;

    },

    error: () => {

      this.loading = false;

    }

  });

  } }
