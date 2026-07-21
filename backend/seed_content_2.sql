-- Additional ShebaPath guides — safe to re-run (upserts by slug)

INSERT INTO bd_guides (slug, category, title, summary, steps, requirements, fees, processing_time, office, published_at)
VALUES
(
    'land-mutation',
    'Land & Property',
    'How to Apply for Land Mutation (e-Namjari)',
    'Transfer land ownership records into your name online through the e-Mutation (e-Namjari) system.',
    '["Apply online at the Ministry of Land''s e-Mutation portal (land.gov.bd / mutation.land.gov.bd) with your deed and Khatian details", "Pay the court fee (৳20) and notice-issuance fee (৳50) online", "Wait for a hearing/verification notice from the Assistant Commissioner (Land)", "Attend the hearing (in person or, where available, online) if you are called", "Once approved, pay the record-correction and mutation ledger fee (৳1,100) online", "Download your updated Khatian (record of rights) and DCR (Duplicate Carbon Receipt)"]',
    '["Certified copy of your registered deed", "Previous (via) deed, if the property changed hands before", "Existing Khatian/porcha copy", "Latest land development tax receipts", "Applicant''s NID, TIN and passport-size photo"]',
    'Around ৳1,170 total (৳20 court fee + ৳50 notice fee + ৳1,000 record correction + ৳100 ledger copy), paid online via mobile or internet banking — no cash accepted at the land office',
    'Roughly 28 days on average, usually needing only one in-person visit',
    'Assistant Commissioner (Land) Office / Union or Upazila Land Office, via land.gov.bd',
    now()
),
(
    'tin-registration',
    'Tax',
    'How to Register for an e-TIN',
    'Get your Tax Identification Number online through the National Board of Revenue (NBR) e-TIN portal.',
    '["Go to the NBR e-TIN portal at secure.incometax.gov.bd/TINHome", "Register using your NID number and an active mobile number", "Your NID details are verified electronically — no physical documents needed for most individuals", "Fill in your personal or business details and select your taxpayer category", "Submit the form to receive your 12-digit TIN certificate, usually instantly", "Download and save your TIN certificate as a PDF for future use"]',
    '["Valid NID (for individuals)", "Active mobile number and email address not already used for another TIN", "For businesses: trade license / certificate of incorporation and authorized signatory details"]',
    'Free — NBR does not charge any fee for e-TIN registration; be cautious of agents or third-party sites demanding payment for this step',
    'Usually instant for individuals; 1–3 working days for companies that need RJSC record cross-checks',
    'National Board of Revenue (NBR) — fully online, no office visit required',
    now()
),
(
    'trade-licence-renewal',
    'Business',
    'How to Renew Your Trade License',
    'Keep your business legally operating by renewing your trade license before the fiscal-year deadline.',
    '["Collect the renewal form from your City Corporation/Municipality zonal office, or use their online portal if available", "Fill in the form and attach your previous license book and last payment challan", "Submit the renewal application at the zonal office or online", "Pay the renewal fee at the designated bank or via the online gateway", "Collect your renewed license book or updated digital license"]',
    '["Previous trade license book", "Rent receipt or ownership proof of the business premises", "TIN certificate", "Last renewal''s payment challan"]',
    'Roughly ৳500 for small traders up to ৳50,000+ for larger enterprises, depending on business category and capital',
    '7–15 working days',
    'Dhaka North/South City Corporation or local Municipality/Union Parishad zonal office',
    now()
)
ON CONFLICT (slug) DO UPDATE SET
    category = EXCLUDED.category,
    title = EXCLUDED.title,
    summary = EXCLUDED.summary,
    steps = EXCLUDED.steps,
    requirements = EXCLUDED.requirements,
    fees = EXCLUDED.fees,
    processing_time = EXCLUDED.processing_time,
    office = EXCLUDED.office;
