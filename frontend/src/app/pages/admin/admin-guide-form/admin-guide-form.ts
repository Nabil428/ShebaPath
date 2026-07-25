import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { GuidesService } from '../../../core/services/guides.service';
import { AdminService } from '../../../core/services/admin.service';
import { Category } from '../../../core/models/models';

function linesToArray(text: string): string[] {
  return text.split('\n').map((s) => s.trim()).filter(Boolean);
}
function csvToArray(text: string): string[] {
  return text.split(',').map((s) => s.trim()).filter(Boolean);
}

@Component({
  selector: 'app-admin-guide-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './admin-guide-form.html',
  styleUrl: './admin-guide-form.scss',
})
export class AdminGuideFormPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly guidesService = inject(GuidesService);
  private readonly admin = inject(AdminService);

  isEdit = false;
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);

  readonly form = this.fb.nonNullable.group({
    slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
    categoryId: [0, [Validators.required, Validators.min(1)]],
    title: ['', Validators.required],
    summary: ['', Validators.required],
    fees: [''],
    processingTime: [''],
    office: [''],
    featuredImage: [''],
    keywords: [''],
    metaDescription: [''],
    isFeatured: [false],
    isPublished: [true],
    tagsText: [''],
    stepsText: ['', Validators.required],
    requirementsText: ['', Validators.required],
  });

  ngOnInit(): void {
    this.admin.getCategories().subscribe((cats) => this.categories.set(cats));

    const slug = this.route.snapshot.paramMap.get('slug');
    if (slug && slug !== 'new') {
      this.isEdit = true;
      this.form.controls.slug.disable();
      this.guidesService.get(slug).subscribe((g) => {
        this.form.patchValue({
          slug: g.slug,
          categoryId: g.categoryId ?? 0,
          title: g.title,
          summary: g.summary,
          fees: g.fees ?? '',
          processingTime: g.processingTime ?? '',
          office: g.office ?? '',
          tagsText: g.tags.join(', '),
          stepsText: g.steps.join('\n'),
          requirementsText: g.requirements.join('\n'),
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
      categoryId: Number(v.categoryId),
      title: v.title,
      summary: v.summary,
      fees: v.fees || undefined,
      processingTime: v.processingTime || undefined,
      office: v.office || undefined,
      featuredImage: v.featuredImage || undefined,
      keywords: v.keywords || undefined,
      metaDescription: v.metaDescription || undefined,
      isFeatured: v.isFeatured,
      isPublished: v.isPublished,
      tags: csvToArray(v.tagsText),
      steps: linesToArray(v.stepsText),
      requirements: linesToArray(v.requirementsText),
    };

    const request = this.isEdit
      ? this.admin.updateGuide(v.slug, payload)
      : this.admin.createGuide(payload);

    request.subscribe({
      next: () => this.router.navigate(['/admin']),
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.error ?? 'Could not save this guide.');
      },
    });
  }
}
