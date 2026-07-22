import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BlogService } from '../../core/services/blog.service';
import { BlogSummary } from '../../core/models/models';

const PAGE_SIZE = 5;

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
  readonly searchTerm = signal('');
  readonly page = signal(1);

  readonly filtered = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return this.posts();
    return this.posts().filter(
      (p) => p.title.toLowerCase().includes(term) || p.excerpt.toLowerCase().includes(term)
    );
  });

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / PAGE_SIZE)));

  readonly pageItems = computed(() => {
    const start = (this.page() - 1) * PAGE_SIZE;
    return this.filtered().slice(start, start + PAGE_SIZE);
  });

  readonly pageNumbers = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    this.blogService.list().subscribe((posts) => {
      this.posts.set(posts);
      this.loading.set(false);
    });
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
    this.page.set(1);
  }

  goToPage(n: number): void {
    if (n >= 1 && n <= this.totalPages()) {
      this.page.set(n);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }
}
