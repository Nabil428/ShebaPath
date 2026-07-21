-- ShebaPath content seed — guides & blog posts
-- Safe to re-run: uses ON CONFLICT to update existing rows instead of erroring.

-- =========================
-- GUIDES
-- =========================

INSERT INTO bd_guides (slug, category, title, summary, steps, requirements, fees, processing_time, office, published_at)
VALUES
(
    'driving-licence',
    'Transport',
    'How to Apply for a Driving Licence',
    'Step-by-step guide to getting a learner and then a full driving licence from BRTA.',
    '["Register and create an account on the BRTA online portal (brta.gov.bd)", "Fill in the learner licence application and upload your documents", "Pay the fee online or through a designated bank", "Book a slot for the written and practical (road) test", "Pass the written, oral and practical tests at the BRTA circle office", "Collect your smart card driving licence (or check delivery status online)"]',
    '["Valid NID (or birth certificate if under 18 with guardian consent)", "Recent passport-size photographs", "Medical fitness certificate from a registered physician", "Learner licence (for the full licence stage)"]',
    '৳2,542 for a learner licence (light vehicle); ৳3,542–৳5,542 for a full smart card licence depending on category — always confirm the current fee schedule on brta.gov.bd before paying',
    '30–45 days after passing the practical test for the smart card to be printed and delivered',
    'BRTA Circle/Regional Office (based on your address)',
    now()
),
(
    'passport',
    'Travel',
    'How to Apply for an e-Passport',
    'Complete walkthrough for applying for a Bangladeshi e-Passport, from the online form to collection.',
    '["Create an account and complete the application at epassport.gov.bd", "Choose the page count (48/64) and validity (5/10 years) and pay the fee online, by bank, or via mobile banking", "Book an appointment at your nearest Passport Office", "Visit in person for photo, fingerprints, iris scan and signature", "Wait for police verification if your application requires it", "Track your status online and collect the passport (or request home delivery where available)"]',
    '["NID (for applicants 18 and above) or Birth Certificate (under 18)", "Previous passport, if renewing", "Payment receipt/slip", "Passport-size photo may be needed only for specific categories — the system captures your photo live in most cases"]',
    '৳4,025–৳12,075 depending on page count (48/64), validity (5/10 years) and delivery speed (regular, express or super express) — see epassport.gov.bd for the exact current fee table',
    'Around 15–21 working days for regular delivery; 2–4 working days for super express; longer if police verification is required',
    'Department of Immigration & Passports (DIP) — Regional Passport Office',
    now()
),
(
    'national-id',
    'Identity',
    'How to Correct or Reissue Your National ID (NID)',
    'How to fix errors on your NID card or apply for a reissue through the Election Commission portal.',
    '["Log in or register at services.nidw.gov.bd using your NID number and mobile number", "Select the type of correction needed (name, date of birth, address, etc.) or choose reissue for a lost/damaged card", "Upload supporting documents that justify the correction", "Pay the applicable fee via bKash, Rocket, Nagad or a designated bank", "Track your application status online", "Collect the corrected or reissued card from your Upazila/District Election Office when notified"]',
    '["Existing NID number and registered mobile number", "Supporting documents (e.g. SSC certificate, birth certificate, passport) proving the correct information", "Payment receipt"]',
    'Correction: ৳230 (first time), ৳345 (second time), ৳575 (each time after); Reissue of a lost/damaged card: around ৳200 (regular) to ৳300 (urgent) — confirm on services.nidw.gov.bd',
    'Roughly 30 days for online corrections; can extend to about 2 months for complex cases',
    'Bangladesh Election Commission — Upazila/District Election Office',
    now()
),
(
    'birth-certificate',
    'Civil Registration',
    'How to Register or Correct a Birth Certificate',
    'Guide to registering a new birth or correcting details on an existing certificate via BDRIS.',
    '["Apply online at bdris.gov.bd/br/application", "Enter the child''s (or applicant''s) details and parents'' information exactly as they should appear", "Pay the registration or correction fee online or at the local registrar''s office", "Print the completed application form", "Submit the printed form with supporting documents to your Union Parishad / City Corporation / Pourashava office", "Collect the certificate or download the verified digital copy once ready"]',
    '["Hospital or attendant birth certificate, or a sworn affidavit if born at home", "Parents'' NID or their own birth certificates", "Proof of address (utility bill, holding tax receipt, etc.)"]',
    'Free if registered within 45 days of birth; ৳50 after 45 days; ৳100 late fee for correcting the date of birth — see bdris.gov.bd for the full fee schedule',
    'Typically 3–7 working days after the printed form and documents are submitted',
    'Union Parishad / City Corporation / Pourashava Registrar''s Office',
    now()
),
(
    'trade-licence',
    'Business',
    'How to Get a Trade License for Your Business',
    'Step-by-step process for obtaining a new trade license from your City Corporation or Union Parishad.',
    '["Collect the application form (Form K for commercial, Form I for manufacturing) from your zonal City Corporation/Municipality office", "Fill in the form and attach the required documents", "Submit the application to the concerned zonal office", "A Licensing Supervisor inspects the business premises for verification", "Pay the assessed fee — license fee plus a 30% signboard fee — at the designated bank", "Collect your trade license"]',
    '["NID of the business owner/directors", "Rental agreement or ownership proof for the business premises", "TIN certificate", "Passport-size photographs", "For renewals: previous license book and payment challan"]',
    '৳10 application fee, plus a license fee that ranges roughly from ৳100 to ৳40,000+ depending on business type and capital, plus a 30% signboard fee on the license amount',
    '3–15 working days for most general/commercial licenses; up to 30 days for manufacturing units requiring inspection',
    'Dhaka North/South City Corporation or the relevant Municipality/Union Parishad zonal office',
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

-- =========================
-- BLOG POSTS
-- =========================

INSERT INTO bd_blog_posts (slug, title, excerpt, content, cover_image_url, published_at)
VALUES
(
    'common-mistakes-epassport-application',
    '5 Common Mistakes That Delay Your e-Passport',
    'Avoid these frequent errors that push back e-Passport processing by weeks.',
    E'Applying for an e-Passport is straightforward on paper, but small mistakes can add weeks to your wait. Here are the most common ones:\n\n1. Mismatched name spelling — Your name on the passport application must exactly match your NID or birth certificate. Even a missing middle initial can trigger a rejection.\n\n2. Wrong address details — Use your NID address unless you have valid proof of a new address (utility bill, rental agreement). Inconsistent addresses commonly trigger extra police verification.\n\n3. Skipping the appointment slot — Walking in without a booked appointment often means a longer wait or being turned away. Always book through the portal first.\n\n4. Incomplete online payment — If your payment doesn''t reflect in the system before your appointment, your file won''t be processed. Keep your payment slip and confirmation SMS as proof.\n\n5. Not tracking application status — Passports needing police verification can sit idle if you don''t follow up. Check your status online regularly and contact the regional office if there''s no movement after 3 weeks.\n\nPlanning ahead and double-checking your documents against your NID before you submit can save you a lot of back-and-forth.',
    null,
    now()
),
(
    'how-long-government-services-take',
    'How Long Do Government Services in Bangladesh Really Take?',
    'A realistic look at processing times for passports, NID corrections, birth certificates and more.',
    E'Official timelines and real-world experience don''t always match. Here''s a general sense of what to expect for the services covered on ShebaPath:\n\n- Driving Licence: The test itself can be booked within a couple of weeks, but the physical smart card often takes 30–45 days to arrive after you pass.\n\n- e-Passport: Regular delivery is usually 15–21 working days if no police verification is needed. If verification is triggered, add another 2–4 weeks.\n\n- NID Correction: Straightforward corrections (like a spelling fix) typically clear in about a month. Corrections involving date of birth or more significant edits can take longer and sometimes require in-person verification.\n\n- Birth Certificate: The fastest of the group — usually 3–7 days once your printed form and documents are submitted to your local registrar.\n\n- Trade License: General and commercial licenses are often issued within 1–2 weeks. Manufacturing licenses requiring a fire/environmental inspection can take up to a month.\n\nA good rule of thumb: apply well ahead of any deadline (travel, school admission, business launch), and always keep your payment receipts and application numbers so you can follow up if something stalls.',
    null,
    now()
)
ON CONFLICT (slug) DO UPDATE SET
    title = EXCLUDED.title,
    excerpt = EXCLUDED.excerpt,
    content = EXCLUDED.content,
    cover_image_url = EXCLUDED.cover_image_url;
