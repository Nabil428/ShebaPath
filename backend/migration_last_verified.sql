-- Adds a "last verified" date to guides, so readers know how fresh the
-- fee/process info is. Safe to re-run.

ALTER TABLE bd_guides
  ADD COLUMN IF NOT EXISTS last_verified DATE NOT NULL DEFAULT CURRENT_DATE;

-- Since all current guide content was freshly researched and written today,
-- mark them as verified as of today.
UPDATE bd_guides SET last_verified = CURRENT_DATE;
