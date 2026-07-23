# Nonprofit tech entity setup (US) — Liberation Fleet

**Not legal advice.** Formation, tax exemption, and solicitation rules vary by state and facts. Confirm with a nonprofit attorney and CPA before filing. This guide matches the product’s [jurisdiction assumptions](./JURISDICTION-ASSUMPTIONS.md) (US-first, 18+) and donation posture in [DONATION-SETUP.md](./DONATION-SETUP.md).

Use this when launch checklist item **A — Form legal entity** means a **nonprofit**, not an LLC/for-profit.

---

## First decision: nonprofit vs for-profit (or both)

| Path | When it fits Liberation Fleet | Main tradeoff |
|------|-------------------------------|---------------|
| **501(c)(3) public charity** | Mission is charitable/educational (mutual aid coordination, community resilience education, reducing isolation) and you want tax-deductible donations | Strict limits on politics/lobbying; private benefit forbidden; IRS scrutiny of “software company” activity |
| **501(c)(4) social welfare** | Mission is civic / social welfare and you need more advocacy room | Donations generally **not** tax-deductible; different donor expectations |
| **For-profit LLC / C-corp** | Product is a commercial SaaS; nonprofit branding would be a stretch | Simpler commercial ops; no charitable deduction for donors |
| **Hybrid** | Nonprofit owns mission + IP licensing; separate LLC runs commercial hosting (advanced) | Flexible but costly; needs careful contracts |

**Do not** form a 501(c)(3) only to look good in App Store / Stripe. Exemption must match real purpose and operations.

---

## Recommended default for this app

If the public story is **charitable mutual-aid infrastructure + education**, start planning as:

1. **State nonprofit corporation** (membership or board-only — see below)
2. Apply for **IRS 501(c)(3)** recognition (usually Form **1023** or **1023-EZ** if eligible)
3. Register as a **charity where you solicit** donations (state AG / charities bureau)
4. Then wire Stripe / bank / stores to that entity

If counsel says the product is too commercial for (c)(3), stop and reconsider LLC or (c)(4) before spending months on 1023.

---

## Step 1 — Clarify purpose in one sentence

Write a purpose you can put in Articles and Form 1023, for example:

> Organize and educate communities for mutual aid, resource sharing, and emergency preparedness through privacy-respecting software and related programs.

**Do**

- Emphasize **charitable / educational** ends (relief, education, community benefit)
- Keep the **app as a means**, not “we sell a social network”

**Don’t**

- Lead with “build a profitable consumer app”
- Promise tax deductions to users in-app before exemption is granted ([DONATION-SETUP.md](./DONATION-SETUP.md) already warns the app does not issue tax receipts)

---

## Step 2 — Pick state of incorporation

Common choices:

| Choice | Why people pick it | Watch-outs |
|--------|--------------------|------------|
| **Your home state** | Cheapest compliance if you live/operate there | Fine for most startups |
| **Delaware** | Familiar corporate law | Still register to do business / solicit in states where you operate |
| **Avoid “random” states** | No magic tax win for 501(c)(3) | Extra foreign-qualification filings |

**Do:** Incorporate where founders live **or** where counsel already practices.  
**Don’t:** Assume Delaware nonprofit = easier IRS approval.

---

## Step 3 — Choose corporate form + governance

### Board

- Minimum often **3 unrelated directors** (state + IRS best practice; some states allow fewer for nonprofits — still use 3+)
- Written **conflict-of-interest** policy
- Documented meetings / consents

### Membership vs non-membership

| Model | Fits when |
|-------|-----------|
| **Board-only (non-membership)** | Simpler; board elects successors — usual for software orgs |
| **Membership** | Users/crews are legal members with voting rights — rare for apps; high admin load |

**Do:** Start **board-only** unless counsel has a reason for membership.  
**Don’t:** Make every app user a corporate “member” by accident in bylaws.

---

## Step 4 — IRS category / NTEE (how you “categorize”)

### Primary exemption type

For Liberation Fleet–style mutual aid + education, Form 1023 usually argues **charitable** and/or **educational** under **501(c)(3)**.

| Classification | Use if… | Avoid if… |
|----------------|---------|-----------|
| **Charitable** (relief of poor/distressed, community benefit) | Mutual aid, emergency support networks | You’re mostly a paid social club |
| **Educational** | Teaching preparedness, financial literacy, community organizing | Content is pure entertainment with no education |
| **Religious** | Only if you are actually a church/religious org | App is secular mutual aid — don’t force this |
| **501(c)(4)** | Civic/social welfare + more lobbying | You need deductible donations |
| **501(c)(7) social club** | Private club for members’ pleasure | Public app + public fundraising — wrong box |

### NTEE / activity codes (examples — confirm with preparer)

Pick codes that match **mission**, not “tech”:

| Code family (examples) | Why it might fit |
|------------------------|------------------|
| **P20 / P80** (Human Services — mutual aid / neighborhood centers style) | Mutual aid coordination |
| **M20** (Disaster Preparedness & Relief) | Emergency / readiness angle |
| **W90 / W99** (Public / Societal Benefit — community improvement) | Civic resilience |
| **B90** (Educational Services) | If education is primary |
| **U40 / U41** (Engineering / technology) | Only as **supporting** activity — don’t make “we’re a tech company” the charity purpose |

**Do:** Primary NTEE = **human services / community / disaster prep**, with tech as method.  
**Don’t:** Primary NTEE = generic “software company” or “social media.”

### Public charity vs private foundation

Most operating nonprofits want **public charity** status (public support test or automatic categories), not private foundation.

**Do:** Plan diversified public support (many small donors, grants).  
**Don’t:** Fund 100% from one founder forever without CPA modeling (can look like a private foundation).

---

## Step 5 — Name, articles, bylaws (state filing)

1. Check name availability with the **state corporations** office.
2. Draft **Articles of Incorporation** (nonprofit) including:
   - Purpose clause aligned with 501(c)(3)
   - **Dissolution clause** dedicating assets to another 501(c)(3)
   - Any state-required language for tax exemption
3. File Articles; pay fee; receive stamped copy / entity ID.
4. Adopt **bylaws**, conflict policy, whistleblower / document retention (board resolutions).
5. Hold organizational meeting: elect officers, authorize EIN, bank, fiscal year.

**Do:** Use a nonprofit-specific articles template from counsel or a reputable state-specific kit.  
**Don’t:** File LLC articles “and fix it later.”

---

## Step 6 — EIN (IRS)

1. [IRS EIN online](https://www.irs.gov/businesses/small-businesses-self-employed/apply-for-an-employer-identification-number-ein-online) (responsible party with SSN/ITIN).
2. Entity type: **Nonprofit / corporation / other** as prompted — match your state filing.
3. Save the CP 575 / EIN confirmation PDF.

**Do:** Get EIN **after** state formation (or same day once Articles are filed).  
**Don’t:** Reuse a personal EIN or a dissolved entity’s EIN.

---

## Step 7 — Federal tax exemption (501(c)(3))

### 7.1 Choose form

| Form | Rough fit |
|------|-----------|
| **1023-EZ** | Small orgs under IRS gross-receipts / asset thresholds; simple activities — **many tech platforms do not qualify**; confirm eligibility worksheet |
| **1023** (full) | Safer default for a software + fundraising + multi-state app |

### 7.2 What to describe (do’s / don’ts in narrative)

**Do describe**

- Charitable/educational programs (training, mutual-aid facilitation, preparedness resources)
- Who is served (communities, crews — public benefit, not private social club)
- How donations are used (hosting, moderation safety, program staff, grants to users if any)
- Privacy / safety as **protecting users**, not as a commercial differentiator only
- Fee policy: if any paid features exist, explain they are **related** to exempt purpose or treated as unrelated business income (UBI) with CPA

**Don’t describe**

- “Exit,” equity, investors, or founder appreciation rights
- Unlimited private benefit to founders (salary must be reasonable and approved)
- Partisan campaign intervention (501(c)(3) prohibition)
- “We’ll decide purpose later”

### 7.3 After filing

- Wait for **determination letter** (can take months on Form 1023)
- You may operate as nonprofit corporation meanwhile; **tax-deductible donation claims** usually wait until exemption is effective (ask CPA about “pending” solicitations)

---

## Step 8 — State charity / fundraising registration

If you **solicit donations** online from residents of a state, that state may require **charitable solicitation registration** (AG or charities bureau) **before** or soon after fundraising — independent of IRS status.

**Do**

- Register in your home state first
- Budget for multi-state registration or a unified portal / counsel if you fundraise nationally
- Align website / app donation copy with registered legal name

**Don’t**

- Put “donate” live nationwide with zero charity registrations
- Assume “we’re only a tech platform” skips solicitation law

---

## Step 9 — Banking, accounting, insurance

1. Board resolution to open a bank account in the **legal nonprofit name** + EIN.
2. Accounting: separate books; track restricted vs unrestricted gifts.
3. Fiscal year choice (calendar is simplest).
4. Insurance: D&O, general liability, cyber (you run a social + report/safety stack).
5. Annual filings: IRS **Form 990** series; state annual reports; charity renewals.

**Do:** Pay founders/contractors only with board-approved, documented, reasonable amounts.  
**Don’t:** Comingle personal and org funds or treat Azure / Apple bills as personal expenses without reimbursement policy.

---

## Step 10 — Wire the entity into Liberation Fleet ops

| System | What to use |
|--------|-------------|
| **Azure / Microsoft** | Billing account under org; see [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) |
| **Stripe** | Nonprofit / org onboarding; no in-app “tax deductible” claims unless counsel + CPA approve ([DONATION-SETUP.md](./DONATION-SETUP.md)) |
| **Apple Developer / Google Play** | Organization account with D-U-N-S (legal entity name must match) — [STORE-SUBMISSION.md](./STORE-SUBMISSION.md) |
| **Domain / email** | `privacy@`, `support@` on org domain — [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md) §A |
| **NCMEC ESP** | Register the **organization** that operates the service — [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md) |
| **Privacy / Terms** | Publisher = legal nonprofit name; publish HTTPS URLs before store submit |

---

## Categorization cheat sheet (quick picks)

| Question | Prefer | Avoid |
|----------|--------|-------|
| IRS box | **501(c)(3)** charitable + educational | Calling it a social club or pure trade association |
| NTEE primary | Human services / community / disaster prep | “Software publisher” as the charity |
| App Store org type | Company / organization (nonprofit) | Individual account for a real org |
| Stripe | Company / nonprofit | Personal account for org money |
| Political work | Nonpartisan education only (c)(3) | Endorsing candidates |
| Revenue | Donations + related program service fees | Silent plan to flip to VC equity inside the charity |

---

## Global do’s and don’ts

### Do

- [ ] Use counsel for Articles / 1023 purpose language  
- [ ] Keep a real board and minutes  
- [ ] Separate personal vs org money, domains, and cloud billing  
- [ ] Treat CSAM / safety duties as **org** obligations ([JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md))  
- [ ] Say “donations support the nonprofit’s mission” until deduction language is cleared  
- [ ] Document how the **app advances exempt purposes**  

### Don’t

- [ ] Form (c)(3) for branding while running a private commercial SaaS  
- [ ] Promise donors a tax deduction before you have a determination letter (unless CPA says otherwise)  
- [ ] Pay yourself without board approval / reasonableness  
- [ ] Mix campaign politics into (c)(3) channels  
- [ ] Put the org’s only asset (IP) in a founder’s personal Apple/Google account  
- [ ] Skip state solicitation registration because “it’s just an app”  
- [ ] Use Deep Freeze / report evidence access without vendor + privacy counsel alignment  

---

## Suggested timeline (solo / small team)

| Week | Action |
|------|--------|
| 1 | Purpose statement, board candidates, counsel intro, name check |
| 1–2 | File state Articles; EIN; bylaws; bank |
| 2–4 | Draft Form 1023/EZ + policies; start charity registration research |
| Parallel | Privacy/Terms URLs; Stripe org; Apple/Google org (D-U-N-S can take days–weeks) |
| After determination | Update donation copy if deductible; Form 990 calendar |

---

## When to get professionals (non-optional moments)

| Moment | Who |
|--------|-----|
| Purpose / Articles / dissolution clause | Nonprofit attorney |
| 1023 narrative, public support, UBI risk | Attorney + CPA |
| Multi-state solicitation | Attorney or registration service |
| Paying founders / IP assignment into the nonprofit | Attorney |
| “Tax deductible” UX copy | CPA + attorney |

---

## Related product docs

- [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md) — master list (legal first)
- [DONATION-SETUP.md](./DONATION-SETUP.md) — Stripe; no fake tax receipts
- [STORE-SUBMISSION.md](./STORE-SUBMISSION.md) — org developer accounts
- [JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md) — US-first / 18+
- [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md) — ESP registration as the operator
