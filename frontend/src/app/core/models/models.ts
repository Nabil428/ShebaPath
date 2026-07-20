export interface AppUser {
  id: number;
  email: string;
  fullName: string;
  phone: string | null;
  createdAt: string;
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
}

export interface GuideDetail extends GuideSummary {
  steps: string[];
  requirements: string[];
}

export interface BlogSummary {
  slug: string;
  title: string;
  excerpt: string;
  coverImageUrl: string | null;
  publishedAt: string;
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
