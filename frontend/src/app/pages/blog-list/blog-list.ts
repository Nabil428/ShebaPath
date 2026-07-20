import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BlogService } from '../../core/services/blog.service';
import { BlogSummary } from '../../core/models/models';

@Component({
  selector: 'app-blog-list',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './blog-list.html',
  styleUrl: './blog-list.scss',
})
export class BlogListPage implements OnInit {
  private readonly blogService = inject(BlogService);

  readonly posts = signal<BlogSummary[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.blogService.list().subscribe((posts) => {
      this.posts.set(posts);
      this.loading.set(false);
    });
  }
}
