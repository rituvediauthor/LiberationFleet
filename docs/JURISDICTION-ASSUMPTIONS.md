LIBERATION FLEET — JURISDICTION & SAFETY POSTURE
Last updated: July 16, 2026

## Operating assumptions (confirm with counsel before production launch)

1. Primary market: United States.
2. Users must be at least 18 years of age. Minors are not permitted to use the Service.
3. EU Digital Services Act and UK Online Safety Act are out of scope for this initial safety stack.
   Expand compliance if you deliberately serve those jurisdictions at scale.
4. End-to-end encryption remains the default for crew/fleet content. The platform does not
   proactively scan encrypted content. Actual knowledge of apparent CSAM / child exploitation
   arises when a user submits a Report with decrypted evidence, or when a contracted vendor
   escalates a report as CSAM.

## Counsel confirmation checklist (before go-live)

Have privacy / online-safety counsel confirm:

- [ ] US-first ESP posture and §2258A CyberTipline duties match your launch footprint
- [ ] 18+ Terms / Community Standards wording is adequate (age verification not required for US bare minimum today)
- [ ] Report-packet retention windows (non-CSAM ~90 days; CSAM preserve) are acceptable
- [ ] Account freeze + content quarantine on CSAM reports is an acceptable enforcement design
- [ ] Vendor/contractor access to sealed report packets (when used) is covered in privacy disclosures
- [ ] NCMEC ESP registration is complete before handling live CSAM-category reports in production

This file documents product intent. It is not legal advice.
See docs/SAFETY-REPORTING.md for the engineering index, docs/NCMEC-CSAM-runbook.md for filing, and
docs/REPORT-VENDOR-WEBHOOK.md for outsourced triage.
