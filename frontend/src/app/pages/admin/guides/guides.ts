import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { GuidesService } from '../../../core/services/guides.service';
import { AdminService } from '../../../core/services/admin.service';
import { GuideSummary } from '../../../core/models/models';
import { AdminNavComponent } from '../../../Shared/admin-nav/admin-nav';

@Component({
  selector: 'app-admin-guides',
  standalone: true,
  imports: [RouterLink, DatePipe, AdminNavComponent],
  templateUrl: './guides.html',
  styleUrl: './guides.scss',
})
export class Guides implements OnInit {
  private readonly guidesService = inject(GuidesService);
  private readonly admin = inject(AdminService);

  readonly guides = signal<GuideSummary[]>([]);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.guidesService.list().subscribe((g) => this.guides.set(g));
  }

  remove(slug: string): void {
    if (!confirm('Delete this guide permanently?')) return;
    this.admin.deleteGuide(slug).subscribe(() => this.refresh());
  }
}
