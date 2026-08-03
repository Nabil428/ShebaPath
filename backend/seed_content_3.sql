-- New ShebaPath guides — Marriage Registration, Police Clearance Certificate, e-Return (Income Tax)
-- Safe to re-run (upserts by slug). Uses the current relational schema (category_id, tags via guide_tags).

INSERT INTO bd_guides (slug, category_id, title, summary, steps, requirements, fees, processing_time, office, keywords, meta_description, is_featured, last_verified)
VALUES
(
    'marriage-registration',
    (SELECT id FROM categories WHERE slug = 'civil-registration'),
    'How to Register Your Marriage (Kabin Nama / Marriage Certificate)',
    'Step-by-step guide to legally registering a marriage in Bangladesh through a licensed Kazi office.',
    '["Contact a licensed Nikah Registrar (Kazi) in your area for a Muslim marriage, or the relevant Registrar for Hindu/Christian/Special Marriage Act registration", "Collect and fill out the Kabinnama (Form No. 160) with the Mahr (dower) amount clearly stated", "Bring both parties and at least two witnesses to sign in front of the registrar", "Submit NID copies of both parties and witnesses", "Pay the government registration fee (based on the Mahr amount)", "Collect the registered Kabinnama / marriage certificate, usually within a few days"]',
    '["NID of both parties and witnesses", "Passport-size photos", "Mahr (dower) amount to declare", "For Special Marriage Act: a 30-day public notice period with no objections"]',
    'Muslim marriages: roughly ৳14 per ৳1,000 of Mahr value for the first ৳1–5 lakh, lower rates above that; Hindu/Special Marriage fees vary by registrar — confirm before registering',
    '1–3 working days after documents and fee are submitted',
    'Local Kazi (Nikah Registrar) Office, or the relevant Marriage Registrar for other faiths',
    'marriage registration, kabin nama, nikah registration, marriage certificate bangladesh',
    'How to register a marriage in Bangladesh and get your Kabin Nama / marriage certificate.',
    false,
    now()
),
(
    'police-clearance-certificate',
    (SELECT id FROM categories WHERE slug = 'travel'),
    'How to Get a Police Clearance Certificate (PCC)',
    'Guide to applying online for a Police Clearance Certificate, commonly needed for visas, overseas jobs, and immigration.',
    '["Create an account on the Bangladesh Police online PCC portal (pcc.police.gov.bd)", "Fill in the application form with your personal details, passport number, and the purpose of the certificate", "Upload scanned copies of your passport, NID, a recent photo, and proof of address", "Pay the government fee (৳500) online through the portal", "Wait for local police verification at your present or permanent address", "Download or collect your certificate once approved"]',
    '["Valid passport", "NID", "Recent passport-size photo", "Proof of present/permanent address (utility bill, etc.)", "Purpose of the certificate (visa, job, study, immigration)"]',
    '৳500 (flat government fee) — be cautious of agents charging extra beyond the official portal fee',
    'Typically 7–15 working days, sometimes up to 2–3 weeks if extra verification is needed',
    'Bangladesh Police — fully online application at pcc.police.gov.bd, verified via local police station',
    'police clearance certificate, pcc bangladesh, pcc police gov bd',
    'How to apply online for a Police Clearance Certificate (PCC) in Bangladesh, with fees and processing time.',
    false,
    now()
),
(
    'income-tax-e-return',
    (SELECT id FROM categories WHERE slug = 'tax'),
    'How to File Your Income Tax e-Return',
    'Step-by-step guide to submitting your annual income tax return online through the NBR e-Tax portal.',
    '["Register or log in at the NBR e-Tax portal (etaxnbr.gov.bd) using your TIN", "Select the correct assessment year and return type (regular return, or zero return if your income is below the tax-free threshold)", "Enter your income details — salary, other income sources, and any tax already deducted at source (TDS)", "Enter your investments and eligible rebate details; the portal calculates your rebate and tax automatically", "Declare your schedule of assets and liabilities as of 30 June", "Review the auto-calculated tax, pay any amount due via bank/card/mobile banking, and submit", "Download your acknowledgement receipt and income tax certificate"]',
    '["Valid TIN (e-TIN)", "Salary certificate / income details", "Bank statements", "Investment receipts (for rebate claims)", "Registered mobile number linked to NID (or email registration for expatriates)"]',
    'Filing itself is free; any tax due is calculated automatically based on income slabs and paid through the portal''s payment gateway',
    'Usually well under 30 minutes online for straightforward returns; the annual filing deadline is typically late November',
    'National Board of Revenue (NBR) — fully online at etaxnbr.gov.bd',
    'e-return, income tax bangladesh, etaxnbr, nbr tax filing',
    'How to file your income tax e-Return online in Bangladesh through the NBR e-Tax portal.',
    false,
    now()
)
ON CONFLICT (slug) DO UPDATE SET
    category_id = EXCLUDED.category_id,
    title = EXCLUDED.title,
    summary = EXCLUDED.summary,
    steps = EXCLUDED.steps,
    requirements = EXCLUDED.requirements,
    fees = EXCLUDED.fees,
    processing_time = EXCLUDED.processing_time,
    office = EXCLUDED.office,
    keywords = EXCLUDED.keywords,
    meta_description = EXCLUDED.meta_description,
    last_verified = now();

-- Tags
INSERT INTO tags (name, slug) VALUES
    ('marriage', 'marriage'), ('pcc', 'pcc'), ('visa', 'visa'), ('e-return', 'e-return')
ON CONFLICT (slug) DO NOTHING;

INSERT INTO guide_tags (guide_id, tag_id)
SELECT g.id, t.id FROM bd_guides g, tags t
WHERE (g.slug = 'marriage-registration' AND t.slug = 'marriage')
   OR (g.slug = 'police-clearance-certificate' AND t.slug IN ('pcc', 'visa', 'immigration'))
   OR (g.slug = 'income-tax-e-return' AND t.slug IN ('e-return', 'tin'))
ON CONFLICT DO NOTHING;
