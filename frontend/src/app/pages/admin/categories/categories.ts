import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { Category } from '../../../core/models/models';

@Component({
  selector: 'app-admin-categories',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class Categories implements OnInit {
  private readonly admin = inject(AdminService);

  readonly categories = signal<Category[]>([]);
  readonly newName = signal('');
  readonly newDescription = signal('');
  readonly editingId = signal<number | null>(null);
  readonly editName = signal('');
  readonly editDescription = signal('');
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.admin.getCategories().subscribe((c) => this.categories.set(c));
  }

  create(): void {
    if (!this.newName().trim()) return;
    this.errorMessage.set(null);
    this.admin.createCategory({ name: this.newName().trim(), description: this.newDescription().trim() || undefined }).subscribe({
      next: () => {
        this.newName.set('');
        this.newDescription.set('');
        this.refresh();
      },
      error: (err) => this.errorMessage.set(err.error?.error ?? 'Could not create category.'),
    });
  }

  startEdit(c: Category): void {
    this.editingId.set(c.id);
    this.editName.set(c.name);
    this.editDescription.set(c.description ?? '');
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(id: number): void {
    this.admin.updateCategory(id, { name: this.editName().trim(), description: this.editDescription().trim() || undefined }).subscribe(() => {
      this.editingId.set(null);
      this.refresh();
    });
  }

  remove(id: number): void {
    if (!confirm('Delete this category? Guides using it will need to be reassigned first.')) return;
    this.admin.deleteCategory(id).subscribe({
      next: () => this.refresh(),
      error: (err) => this.errorMessage.set(err.error?.error ?? 'Could not delete — guides may still be using this category.'),
    });
  }
}
