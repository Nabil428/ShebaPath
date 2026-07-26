import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { BlogService } from '../../../core/services/blog.service';
import { AdminService } from '../../../core/services/admin.service';
import { AdminNavComponent } from '../../../Shared/admin-nav/admin-nav';

function csvToArray(text: string): string[] {
  return text.split(',').map((s) => s.trim()).filter(Boolean);
}

@Component({
  selector: 'app-admin-blog-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AdminNavComponent],
  templateUrl: './admin-blog-form.html',
  styleUrl: './admin-blog-form.scss',
})
export class AdminBlogFormPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly blogService = inject(BlogService);
  private readonly admin = inject(AdminService);

  isEdit = false;
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
    title: ['', Validators.required],
    excerpt: ['', Validators.required],
    coverImageUrl: [''],
    tagsText: [''],
    content: ['', Validators.required],
  });

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug');
    if (slug && slug !== 'new') {
      this.isEdit = true;
      this.form.controls.slug.disable();
      this.blogService.get(slug).subscribe((p) => {
        this.form.patchValue({
          slug: p.slug,
          title: p.title,
          excerpt: p.excerpt,
          coverImageUrl: p.coverImageUrl ?? '',
          tagsText: p.tags.join(', '),
          content: p.content,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.errorMessage.set(null);
    this.saving.set(true);
    const v = this.form.getRawValue();
    const payload = {
      slug: v.slug,
      title: v.title,
      excerpt: v.excerpt,
      content: v.content,
      coverImageUrl: v.coverImageUrl || undefined,
      tags: csvToArray(v.tagsText),
    };

    const request = this.isEdit
      ? this.admin.updateBlogPost(v.slug, payload)
      : this.admin.createBlogPost(payload);

    request.subscribe({
      next: () => this.router.navigate(['/admin/blogs']),
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.error ?? 'Could not save this post.');
      },
    });
  }
}
