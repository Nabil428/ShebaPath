-- ShebaPath database schema
-- Run this once in the Neon SQL Editor (or via psql) against the target database.

CREATE TABLE IF NOT EXISTS bd_users (
    id            SERIAL PRIMARY KEY,
    email         TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    full_name     TEXT NOT NULL,
    phone         TEXT,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS bd_guides (
    slug             TEXT PRIMARY KEY,
    category         TEXT NOT NULL,
    title            TEXT NOT NULL,
    summary          TEXT NOT NULL,
    steps            JSONB NOT NULL DEFAULT '[]',
    requirements     JSONB NOT NULL DEFAULT '[]',
    fees             TEXT,
    processing_time  TEXT,
    office           TEXT,
    published_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS bd_blog_posts (
    slug             TEXT PRIMARY KEY,
    title            TEXT NOT NULL,
    excerpt          TEXT NOT NULL,
    content          TEXT NOT NULL,
    cover_image_url  TEXT,
    published_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
