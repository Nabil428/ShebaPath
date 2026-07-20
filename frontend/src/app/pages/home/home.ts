import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GuidesService } from '../../core/services/guides.service';
import { BlogService } from '../../core/services/blog.service';
import { GuideSummary, BlogSummary } from '../../core/models/models';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class HomePage implements OnInit {
  private readonly guidesService = inject(GuidesService);
  private readonly blogService = inject(BlogService);

  readonly guides = signal<GuideSummary[]>([]);
  readonly posts = signal<BlogSummary[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.guidesService.list().subscribe((guides) => this.guides.set(guides.slice(0, 5)));
    this.blogService.list().subscribe((posts) => {
      this.posts.set(posts.slice(0, 3));
      this.loading.set(false);
    });
  }
}
