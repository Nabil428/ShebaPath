import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { HeroSlide } from '../../../core/models/models';

@Component({
  selector: 'app-admin-hero-slider',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './hero-slider.html',
  styleUrl: './hero-slider.scss',
})
export class HeroSlider implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly admin = inject(AdminService);

  readonly slides = signal<HeroSlide[]>([]);
  readonly uploading = signal(false);
  readonly saving = signal(false);
  readonly editingId = signal<number | null>(null);

  readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    subtitle: [''],
    imageUrl: ['', Validators.required],
    buttonText: [''],
    buttonLink: [''],
    displayOrder: [0],
    isActive: [true],
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.admin.getHeroSlides().subscribe((s) => this.slides.set(s));
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.admin.uploadImage(file).subscribe({
      next: (res) => {
        this.form.patchValue({ imageUrl: res.imageUrl });
        this.uploading.set(false);
      },
      error: () => this.uploading.set(false),
    });
  }

  edit(slide: HeroSlide): void {
    this.editingId.set(slide.id);
    this.form.patchValue({
      title: slide.title,
      subtitle: slide.subtitle ?? '',
      imageUrl: slide.imageUrl,
      buttonText: slide.buttonText ?? '',
      buttonLink: slide.buttonLink ?? '',
      displayOrder: slide.displayOrder,
      isActive: slide.isActive,
    });
  }

  resetForm(): void {
    this.editingId.set(null);
    this.form.reset({ title: '', subtitle: '', imageUrl: '', buttonText: '', buttonLink: '', displayOrder: 0, isActive: true });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const v = this.form.getRawValue();
    const payload = {
      guideId: null,
      imageUrl: v.imageUrl,
      title: v.title,
      subtitle: v.subtitle || null,
      buttonText: v.buttonText || null,
      buttonLink: v.buttonLink || null,
      displayOrder: Number(v.displayOrder),
      isActive: v.isActive,
    };

    const id = this.editingId();
    const onDone = {
      next: () => {
        this.saving.set(false);
        this.resetForm();
        this.refresh();
      },
      error: () => this.saving.set(false),
    };

    if (id) {
      this.admin.updateHeroSlide(id, payload).subscribe(onDone);
    } else {
      this.admin.createHeroSlide(payload).subscribe(onDone);
    }
  }

  remove(id: number): void {
    if (!confirm('Delete this slide?')) return;
    this.admin.deleteHeroSlide(id).subscribe(() => this.refresh());
  }
}
