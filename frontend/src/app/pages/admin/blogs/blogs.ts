import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { BlogService } from '../../../core/services/blog.service';
import { AdminService } from '../../../core/services/admin.service';
import { BlogSummary } from '../../../core/models/models';
import { AdminNavComponent } from '../../../Shared/admin-nav/admin-nav';

@Component({
  selector: 'app-admin-blogs',
  standalone: true,
  imports: [RouterLink, DatePipe, AdminNavComponent],
  templateUrl: './blogs.html',
  styleUrl: './blogs.scss',
})
export class Blogs implements OnInit {
  private readonly blogService = inject(BlogService);
  private readonly admin = inject(AdminService);

  readonly posts = signal<BlogSummary[]>([]);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.blogService.list().subscribe((p) => this.posts.set(p));
  }

  remove(slug: string): void {
    if (!confirm('Delete this post permanently?')) return;
    this.admin.deleteBlogPost(slug).subscribe(() => this.refresh());
  }
}
