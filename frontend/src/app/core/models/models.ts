export interface AppUser {
  id: number;
  email: string;
  fullName: string;
  phone: string | null;
  createdAt: string;
  isAdmin: boolean;
}

export interface GuideSummary {
  slug: string;
  category: string;
  title: string;
  summary: string;
  fees: string | null;
  processingTime: string | null;
  office: string | null;
  publishedAt: string;
  lastVerified: string;
  tags: string[];
}

export interface GuideDetail extends GuideSummary {
  categoryId?: number;
  steps: string[];
  requirements: string[];
  keywords?: string | null;
  metaDescription?: string | null;
}

export interface BlogSummary {
  slug: string;
  title: string;
  excerpt: string;
  coverImageUrl: string | null;
  publishedAt: string;
  tags: string[];
}

export interface BlogDetail extends BlogSummary {
  content: string;
}

export interface RegisterPayload {
  email: string;
  password: string;
  fullName: string;
  phone?: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface UpdateAccountPayload {
  fullName: string;
  phone?: string;
}

export interface Category {
  id: number;
  name: string;
  slug: string;
  description: string | null;
}

export interface AdminGuidePayload {
  slug: string;
  categoryId: number;
  title: string;
  summary: string;
  steps: string[];
  requirements: string[];
  fees?: string;
  processingTime?: string;
  office?: string;
  featuredImage?: string;
  keywords?: string;
  metaDescription?: string;
  isFeatured: boolean;
  isPublished: boolean;
  tags: string[];
}

export interface AdminBlogPayload {
  slug: string;
  title: string;
  excerpt: string;
  content: string;
  coverImageUrl?: string;
  tags: string[];
}

export interface DashboardStats {
  totalUsers: number;
  totalGuides: number;
  totalBlogs: number;
  totalBookmarks: number;
}

export interface Tag {
  id: number;
  name: string;
  slug: string;
}

export interface HeroSlide {
  id: number;
  guideId: number | null;
  imageUrl: string;
  title: string;
  subtitle: string | null;
  buttonText: string | null;
  buttonLink: string | null;
  displayOrder: number;
  isActive: boolean;
}

export interface HeroSlidePublic {
  id: number;
  guideSlug: string | null;
  imageUrl: string;
  title: string;
  subtitle: string | null;
  buttonText: string | null;
  buttonLink: string | null;
}
