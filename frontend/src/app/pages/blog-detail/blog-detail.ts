import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { BlogService } from '../../core/services/blog.service';
import { PdfExportService } from '../../core/services/pdf-export.service';
import { BlogDetail } from '../../core/models/models';

@Component({
  selector: 'app-blog-detail',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './blog-detail.html',
  styleUrl: './blog-detail.scss',
})
export class BlogDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly blogService = inject(BlogService);
  private readonly pdfExport = inject(PdfExportService);

  readonly post = signal<BlogDetail | null>(null);
  readonly notFound = signal(false);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.route.paramMap
      .pipe(switchMap((params) => this.blogService.get(params.get('slug')!)))
      .subscribe({
        next: (post) => {
          this.post.set(post);
          this.loading.set(false);
        },
        error: () => {
          this.notFound.set(true);
          this.loading.set(false);
        },
      });
  }

  downloadPdf(): void {
    const post = this.post();
    if (post) {
      this.pdfExport.exportBlogPost(post);
    }
  }
}
