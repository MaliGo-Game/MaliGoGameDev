# MaliGo: Market and Behaviour Research

Phase 1 research report (market-and-behaviour). Written 3 Oct 2026 on branch `agents/beta-finish`.
Read-only on code: nothing under `Assets/`, `ProjectSettings/` or `Packages/` was changed.

**Who it's for:** whoever writes `docs/DESIGN_SPEC.md` (Phase 2) and the scenario and Mali content.

---

## 0. The short version

1. **The players are already in the push moment.** For working South Africans aged 18–29, 56% have used savings
   just to get by (up 10 points), 36% report financial stress (up from 29%), 22% have borrowed for everyday
   expenses, and 43% support family both older and younger than them
   ([Old Mutual Savings & Investment Monitor 2026, via FA News](https://www.fanews.co.za/article/retirement/1357/savings-investments/1359/young-south-africans-want-to-save-but-everyday-survival-is-getting-in-the-way/44310);
   [BusinessDay, 13 Jul 2026](https://www.businessday.co.za/economy/2026-07-13-why-young-south-africans-are-saving-less-despite-financial-goals/)).
   91% have savings goals, so they don't lack intent. They lack slack. MaliGo shouldn't teach "you should save".
   It should let people rehearse the specific moments where the slack disappears.
2. **The best hook is the month-end week.** Chapter 1 should be "the seven days before payday": it starts on the
   18th with R600 in hand and ends the night before the 25th. Each day ends with the reveal. The chapter ends
   with an open question about payday, not a score. That is where Chapter 2 picks up.
3. **People come back without streaks for three reasons:** (a) an unresolved tomorrow (a bill due, a pay-later
   instalment, payday); (b) the player's own goal shown in Rand; (c) Mali remembering what happened yesterday.
   The papers in the repo support dropping streaks and leaderboards, but the evidence is mixed (see §1.3).
   The case rests on wellbeing, not on engagement.
4. **The moat is specific, not generic.** Spent, Payback, Bite Club and BitLife are all US-centric or
   consequence-light. SA bank apps gamify saving for people who already have a surplus. Nobody else rehearses
   taxi fares, black tax, stokvels, mashonisas and debit-order day in Rand with a companion who doesn't judge.
5. **Seven concrete fixes to existing content** (§6.1). The most important: BNPL instalments fall due after a
   seven-day chapter ends, so the player never feels them. And a choice that costs more than the player has is
   quietly made cheaper, because cash and savings are clamped at 0
   (`Assets/Scripts/Scenarios/ScenarioManager.cs:114-115`).

---

## 1. What already existed, and what the 27 papers actually say

### 1.1 Prior synthesis

There isn't one. I searched every tracked `.md/.txt/.cs/.json` for "synthesis", "literature review",
"research summary" and "paper". The only hits are the one-line claims in `docs/MASTER_PROMPT.md` §3 and
`README.md:53-57` ("streaks/leaderboards drive anxiety and false confidence", "knowledge ≠ behaviour",
"feedback must be immediate/specific/causal"). The 27 PDFs in `Behavioural Research/` are also copied untracked
into `docs/research/`. This report is the first written synthesis.

### 1.2 Corpus audit

All 27 PDFs were text-extracted (`pdftotext`). `385887509_FROM_GAMIFICATION...pdf` is image-only, so I read
pages 1–3 visually. About a third of the corpus is relevant to MaliGo's design. The rest is macro-saving
economics or other populations.

| Paper (file in `Behavioural Research/`) | What it gives MaliGo | Relevance |
|---|---|---|
| Kalmi & Rahko 2022, game-based financial education, Finland (n=640, 15-year-olds) | Games raised **knowledge** robustly, but effects on self-reported **behaviour were "weak"/"negligible"**. This is the source of "knowledge isn't behaviour". | High |
| Celestin & Vanitha 2021, `385887509_...pdf` (p.175) | Gamified finance apps: rewards and progress tracking predict proactive behaviour (β=0.42), but there was a **25% rise in confidence despite low literacy scores**. This is the source of the "false confidence" claim. Weak methods (conference paper, unclear sample). | High (claim) / low (rigour) |
| `ijnrefm-volume4-issue4-168.pdf` (2026 review of AI finance apps) | Cites Seaborn & Fels (2015): badges and **savings streaks raised engagement but "elevated competitive anxiety"**, and broken streaks caused "harmful emotional responses" (Potrich et al. 2020). Apps can raise subjective confidence while objective outcomes stagnate. Recommends "subordinating engagement metrics to well-being outcomes". This is the source of the anti-streak rule. | High |
| Mulcahy, Russell-Bennett & Iacobucci 2020, gamified energy app field study (`1-s2.0-S0148296318305071`) | Real customers, real bills. **Feedback → knowledge** and **Character → knowledge** were significant, and both were stronger for **casual** users than hardcore ones. Directly supports Mali (a character) plus causal feedback for a casual phone audience. | High |
| Bisanti et al., Digital Escape Games (Museum of Saving, Turin; players aged 11–14) | Cooperative play helped on harder puzzles. **Counter-evidence:** in the 4-week follow-up, 18 of 20 voluntary re-players came from the group with a weekly leaderboard and prize (154 sessions versus 3). Leaderboards do drive replay. | High (honest counterweight) |
| Rimenda et al. 2022, gamified saving in Indonesian digital bank, millennials (n=172) | Badges were the strongest predictor of intent to save. Challenges were the weakest. Measures intent, not behaviour. | Medium |
| Adepeju et al. 2023, "Gamification of Savings and Investment" (`Paper+9`) | Pro-gamification review that recommends leaderboards and streaks for retention. Flags manipulation and over-gamification risks. Low rigour. | Medium (opposing view) |
| van der Heide & Želinský 2021, "Level up your money game" | Critiques fintech gamification discourse that treats users as "dopamine" machines and frames competition and leaderboards as human nature. Useful for MaliGo's stance and tone. | Medium |
| Lai & Langley 2024, "Playful finance" (Geoforum) | Case studies: UOB's City of TMRW, Razer, Ant Forest. Gamification used to capture attention and lock users into a platform. | Medium (comparables) |
| Hayes, MRP (Ryerson) | Review of budgeting apps (YNAB's "turn categories green" described by users as "addictive"). Includes an appendix of biases: present bias, overconfidence, anchoring, loss aversion. | Medium |
| Santos 2024, DreamScape / Where's the Finance (thesis, young adults) | The narrative game was more immersive. The idle game taught concepts more clearly. **Story carries engagement, and concrete numbers carry learning.** MaliGo needs both: Mali's story plus the Rand reveal. | Medium |
| Platz & Jüttler 2022 (analogue game plus debriefing, n=50) | Situational interest grows most for people who care about money but haven't had a chance to engage. **The debrief matters.** That argues for the chapter-end reflection. | Medium |
| Allal-Chérif et al. 2022, serious games at AXA | If a game is too playful it loses credibility, and if it's too realistic it's boring. Check that learning transfers to the real world. | Medium |
| Struwig, Watson & Abrahams 2023, SA saving-behaviour review | Five drivers for SA: demographics, income, financial literacy, parental influence, life cycle. Focus groups added **culture, ease of debt access, saving incentives, peer influence**. | High (SA) |
| Baloyi (TUT thesis), township household savings | Stokvel members had higher odds of having savings (non-members AOR 0.31, p<0.001). Focus groups: people know stokvels but fear **"Kipi" and WhatsApp stokvel scams**. Workplace-compulsory saving is the only saving many can name. | High (SA) |
| Rulashe et al. 2025, SA savings trends 1980–2023 | Names **black tax** (supporting extended family) as a disruption of the life-cycle model for young earners. Stokvels are trusted, culturally aligned saving, and a sign of distrust in formal banks. | High (SA) |
| Simatele & Maciko 2022, rural SA financial inclusion (JRFM) | Fees that are complex and opaque create distrust. A focus-group quote: "I am not at that level, **Omashonisa** works well for me." Funeral cover is the most-used insurance. | High (SA) |
| Ntieche & Nzepang 2026, digital savings and emergencies (SSA) | Precautionary digital savings help households absorb health and education shocks. Supports the emergency-expense scenario. | Medium |
| Loaba 2022, mobile banking in West Africa | Mobile banking slightly raises formal and informal saving. Context only. | Low |
| IJAMC 2025 adaptive saving-challenge app | Small n=50 satisfaction study. Context only. | Low |
| Lindqvist 1981; Juster & Taylor 1975; Collins 1991 (NBER); Prinsloo 2000 (SARB OP14); Jumena et al. 2022 SLR; Vu Thi Kim et al. 2025 (elderly, Hanoi); fpsyg 2022 (Saudi women entrepreneurs) | Macro or other-population saving determinants. Useful background (uncertainty raises precautionary saving, and debt access lowers saving) but no design mechanics. | Low |

### 1.3 Do the papers support "no streaks, no leaderboards"?

**Partly. The spec should be honest about this.**

- **For the rule:** `ijnrefm` (streaks → competitive anxiety, broken streaks → harm), Celestin & Vanitha (confidence
  rises faster than competence), van der Heide & Želinský (competition framing is industry ideology, not evidence).
  Outside the corpus: Silverman & Barasch (Journal of Consumer Research, 2023) found that highlighting a **broken**
  streak lowers further engagement compared with an intact one. The streak's value is all downside once it breaks
  ([CU Boulder summary](https://www.colorado.edu/business/faculty-research/2023/04/19/or-track-how-broken-streaks-affect-consumer-decisions)).
  A 2024 preprint on run-streakers documents the same backfire after long streaks break
  ([medRxiv](https://www.medrxiv.org/content/10.1101/2024.12.26.24319676.full.pdf)).
- **Against the rule (as a retention tool):** the escape-game leaderboard group replayed about 50× more. Rimenda found
  badges motivate. Duolingo publicly credits the streak for habit
  ([Duolingo blog](https://blog.duolingo.com/how-duolingo-streak-builds-habit)).
- **Why MaliGo should still say no:** MaliGo's audience is under real money stress (36% stressed, 56% raiding
  savings). A mechanic that adds loss-aversion pressure to a money product pushes the same anxiety the product
  is meant to relieve. And in money contexts, a leaderboard ranks people by how much slack they have. That is
  mostly income and family, not skill. Retention has to come from curiosity about tomorrow and from the
  player's own goal (§2, mechanics B4–B6). Per MASTER_PROMPT §5, keep a switch so that a "plain default"
  versus "with feature" A/B test stays possible later.

---

## 2. Real South African push moments (18–30)

**Push** is the moment that sends someone looking. Each row is a push moment. "MaliGo use" says how it becomes
a scenario or a mechanic. Statistics are from sources fetched this session unless marked.

| Push moment | What the numbers say | MaliGo use |
|---|---|---|
| **Month-end before payday** | 76% "regularly run out of money before month-end", and more than half by mid-month ([Floatpays survey via News24, 2023: sponsored content, treat as indicative](https://www.news24.com/brandstory/partner-content/76-of-south-africans-run-out-of-money-before-month-end-realitycheck-20230707)). Nearly half of salary earners have less than R1,000 or a negative balance by payday (Standard Bank, analysis of 402,000 clients, [EWN, Oct 2024](https://www.ewn.co.za/2024/10/21/more-month-at-the-end-your-money-survey-reveals-nearly-half-of-sa-salary-earners-are-broke-before-payday)). 72% report financial stress ([BusinessDay, Jul 2026, DebtBusters](https://www.businessday.co.za/economy/2026-07-22-financial-stress-chokes-south-africans-as-debt-and-living-costs-rise/)). | **The frame for Chapter 1**: seven days from the 18th to the 24th, with payday on the 25th. |
| **Payday spending** | Debit orders cluster on the 25th and 1st. 22% of 18–29s borrowed for everyday costs (OMSIM 2026). Payday is the "fresh start" moment (Dai, Milkman & Riis 2014, below). | Chapter 2 opener: "the first hour of payday" (§6.3, scenario N7). The chapter-end question sets it up. |
| **Taxi fare increases** | 15 million people use minibus taxis daily. Fares rose R2–R6 on local routes and R10–R30 on long-distance routes after the 2026 fuel spike (93 petrol +R6.53/l). Low-income Gauteng metro residents spend about **29% of income on transport** ([Daily Maverick, 19 May 2026](https://www.dailymaverick.co.za/article/2026-05-19-sa-commuters-cut-back-on-groceries-as-taxi-fares-rise-drivers-plead-for-subsidies/); [IOL, SANTACO, Apr 2025](https://iol.co.za/business/economy/2025-04-11-santaco-warns-south-african-commuters-of-imminent-taxi-fare-hikes-following-vat-increase/)). A commuter quote: "It starts with petrol, but it ends with food." | New scenario N2 (fare goes up R2 each way). This matches the existing R15 fare at `ScenarioLibrary.cs:129`. |
| **Airtime and data** | 1GB of 30-day prepaid data averages about R79 (2025), and Cell C charges R85 ([MyBroadband](https://mybroadband.co.za/news/broadband/628760-data-prices-plummet-in-south-africa.html); [ICASA tariff report 2025/26 Q4](https://www.icasa.org.za/uploads/files/2025-26-FY-Q4-Bi-Annual-Tariff-Analysis-Report-For-Publication-23032026.pdf)). For young people, data and airtime are the biggest "luxury" spend (about 40% of it), and 82% of respondents earn under R6,000 a month ([Zaka Index 2025](https://wafunda.com/wp-content/uploads/2025/08/Zaka-Index-2025.pdf)). | The existing R60 weekly airtime bill (`ObligationDefaults.cs:17`), plus new scenario N1: data runs out mid-week. |
| **Family requests / black tax** | 43% of working 18–29s are "sandwich generation", supporting both older and younger dependants. Helping parents is a top-3 priority (OMSIM 2026). 68% of young Black professionals feel strained by black tax ([Vault22 survey via IOL, Mar 2025: vendor survey](https://iol.co.za/news/south-africa/2025-03-26-is-black-tax-pressure-easing-or-increasing-for-young-professionals-in-sa/)). Academic framing: [Development Southern Africa, "Black Tax: Understanding the financial transfers of the emerging black middle class"](https://www.tandfonline.com/doi/full/10.1080/0376835X.2018.1516545), and Rulashe et al. 2025 in the corpus. | The existing `family_obligation`. Keep it fully non-judgmental, because all three choices are legitimate. Add peer pressure as a separate scenario (N3). |
| **Stokvels** | About 810,000 stokvels, 11 million+ members and about R50bn a year ([IOL, Jul 2025, citing NASASA](https://iol.co.za/news/south-africa/2025-07-27-stokvels-hold-billions-but-are-they-missing-the-bigger-financial-opportunity--national-savings-month/)). **53% of working 18–29s are in a stokvel**, 80% keep cash savings, and 30% use mobile money (OMSIM 2026). Baloyi: members are much more likely to have savings, but scams ("Kipi", WhatsApp stokvels) keep people away. | The existing `stokvel_decision`, fixed so it doesn't paint stokvels as the risky option (§6.1). |
| **Grocery stokvels** | Members save all year and bulk-buy in October–November (Black Friday, Makro specials) for December and the "January" squeeze. One example group went from R4,000 to R13,000 per member per year ([allAfrica, Jul 2026](https://allafrica.com/stories/202607160676.html)). A typical figure is about R500/month → R5,000+ in November ([debtsolutions4u guide](https://www.debtsolutions4u.co.za/article/stokvel-guide-south-africa)). | New scenario N5 (December grocery stokvel). |
| **BNPL** | BNPL's share of online payments doubled from 3% (2024) to 6% (2025) ([BusinessDay, Jan 2026, Payfast](https://www.businessday.co.za/companies/2026-01-20-buy-now-pay-later-use-doubles-in-2025-payfast-reports/)). 45% of BNPL users use it for everyday purchases. Payflex is used by 39% of BNPL users and PayJustNow by 36.9% ([Stitch](https://stitch.money/blog/buy-now-pay-later-in-south-africa-what-stores-should-know)). Merchants report 20–30% bigger baskets ([Payflex](https://payflex.co.za/merchant-hub/why-is-buy-now-pay-later-one-of-the-fastest-growing-payment-solutions-in-south-africa/)). | The existing `credit_bnpl`, with its instalment timing fixed (§6.1). |
| **Store cards and credit** | 66% of credit-active Gen Z hold clothing-store accounts. Gen Z took 53% of non-bank personal-loan originations ([TransUnion, Gen Z](https://newsroom.transunion.co.za/gen-z-consumers-start-to-shape-south-african-credit-market-as-they-become-increasingly-credit-active/); [TransUnion Q3 2025](https://newsroom.transunion.co.za/south-africas-credit-market-in-q3-2025-strategic-moves-to-manage-risk/)). For 18–29s: store cards 78%, personal loans 64% (+16 points) (OMSIM 2026). | Chapter 2+: "open a store account for 20% off today". |
| **Mashonisa / loan sharks** | An estimated 40,000–50,000 informal lenders ([crunch.africa](https://crunch.africa/blog/mashonisa/)). Typical charges are **30–50% for a few days to a month**, sometimes higher ([gauteng.net](https://www.gauteng.net/whats-on-g/mashonisa-culture-in-the-townships-informal-lending-vs-formal-loans/); [The Citizen](https://www.citizen.co.za/northern-natal-news/98079/south-africas-loan-sharks-violent-vital/)). Use is rising as formal lenders decline more applicants ([IOL, Oct 2025](https://iol.co.za/saturday-star/news/2025-10-29-illegal-lending-on-the-rise-as-desperate-south-africans-turn-to-mashonisas/)). The corpus quote (Simatele & Maciko) shows it's often the only lender that says yes. | New scenario N4. The repayment is an instalment that falls due before the chapter ends, so the player feels it. |
| **Debit-order bounces** | About 1.2 million of 31 million debit orders go unpaid each month (~3.9%) ([PASA FAQ, Jun 2024](https://pasa.org.za/wp-content/uploads/2024/06/Debit-Order-FAQ.pdf)). Banks earn upwards of R500m a month from bounced debit orders ([Moneyweb](https://www.moneyweb.co.za/news/industry/banks-rake-in-upwards-of-r500m-a-month-from-bounced-debit-orders/)). Unpaid-item fee: **R130** at Standard Bank after 3 free ([Access Account pricing 2025](https://www.standardbank.co.za/static_file/South%20Africa/PDF/Personal%20Pricing/2025/Access_Account_Pricing_Guide_2025.pdf), per search summary) and R30 at Nedbank. | New scenario N6 (gym debit order runs tomorrow). |
| **Bank charges** | Youth accounts are about R49.50/month at Nedbank (ages 18–26) ([Nedbank 2025 fees](https://personal.nedbank.co.za/content/dam/nedbank/pdfs/everyday-banking-fees-2025.pdf)). The corpus (Simatele & Maciko) finds opaque fees drive distrust. | A small recurring Obligation ("Bank fees R50/month") in Chapter 2, and a line in the reveal so it's visible. |
| **Students** | NSFAS living allowance is about R17,160 a year (about R1,716/month over 10 months), and transport is about R8,190 a year ([Varsity Wise, 2026: secondary site, not NSFAS itself](https://www.varsitywise.com/posts/nsfas-allowance-breakdown-for-2026-what-students-will-receive)). The STUDENT starting stats are already near this: income R2,500 (`FinancialStats.cs:31-37`). | A student variant: the allowance is late, so the whole week runs on the R600. |
| **First job / no job** | Unemployment Q1 2026: 15–24 at 60.9%, 25–34 at 40.6% ([Stats SA](https://www.statssa.gov.za/?p=19526)). Youth (15–34) at 47.4% in Q2 2026 ([Mail & Guardian, Aug 2026](https://mg.co.za/news/south-africa/2026-08-13-south-africa-s-unemployment-crisis-deepens-as-jobless-rate-rises-to-33-6/)). National minimum wage is **R30.23/hour** from 1 March 2026 ([Global Business](https://www.globalbusiness.co.za/post/government-announces-new-national-minimum-wage-of-r30-23-per-hour-from-1-march-2026)). | Work pays R150 a shift (`WorkInteraction.cs:15`), which is about 5 hours at minimum wage. Present it as a **casual or part-time shift** so it reads as plausible. A full 8-hour day at minimum wage would be about R242. |

**What this means for the game:** the player is someone who already wants to save. Every day in Chapter 1 should
carry one push moment from this table, and the end-of-day reveal is the pull.

---

## 3. Behavioural design principles → specific MaliGo mechanics

Principles marked † are from well-established literature I know but did not re-fetch this session. Links are DOIs.

| # | Principle | Evidence | Mechanic for MaliGo |
|---|---|---|---|
| B1 | **COM-B** (Capability, Opportunity, Motivation → Behaviour) | Michie et al. 2011† ([doi](https://doi.org/10.1186/1748-5908-6-42)). Already a workshop takeaway (MASTER_PROMPT §5). | **C:** the choice panel shows each option's Rand effect *before* the choice. This exists already: `description = "Cash -R50, Stress -5"`, `ScenarioLibrary.cs:71`. Keep it. **O:** a decision every few minutes, with 2 scenarios + work + sleep per day (§5). **M:** the player's own goal in Rand on the HUD, with no score. |
| B2 | **Fogg B=MAP** (a prompt must arrive when motivation and ability are both there) | Fogg† ([behaviormodel.org](https://behaviormodel.org/)) | Scenario triggers sit at the place where the moment really happens (taxi rank, shop window, home phone call). That is already the design in `ScenarioWorldWiring.cs:28-63`. Add **one** gentle prompt per day ("Something's happening at the rank") so the player never wanders a dead world. |
| B3 | **Knowledge ≠ behaviour** | Kalmi & Rahko 2022 (corpus). Fernandes, Lynch & Netemeyer 2014† meta-analysis: financial education explains about 0.1% of variance in behaviour, and effects decay within months ([doi](https://doi.org/10.1287/mnsc.2013.1849)) | No tips and no quizzes. The lesson is the number moving. Mali says what happened, never what one "should" do. |
| B4 | **Salience of consequences** | Karlan, McConnell, Mullainathan & Zinman 2016† ("Getting to the top of mind": reminders raised savings) ([doi](https://doi.org/10.1287/mnsc.2015.2296)). Mulcahy et al. 2020 (corpus): feedback → knowledge. | **The end-of-day reveal** (§5.4): start → end and every line that moved it, in order. Plus **"tomorrow"** lines: bills and instalments due in the next 2 nights, in Rand. |
| B5 | **Present bias / scarcity** | Laibson 1997† ([doi](https://doi.org/10.1162/003355397555253)). Mullainathan & Shafir 2013† *Scarcity* (cited in `ijnrefm`). Month-end "tunnelling". | Put the costs of present-biased choices on the **visible near-term calendar**: BNPL and mashonisa repayments fall due *inside* the chapter (§6.1). Give the scarcity frame its own setting: the chapter starts with R600 a week before payday. |
| B6 | **Implementation intentions** ("If X, then I'll Y") | Gollwitzer & Sheeran 2006 meta-analysis, 94 studies, **d = 0.65** ([Konstanz](https://www.socmot.uni-konstanz.de/publications/implementation-intentions-and-goal-achievement-meta-analysis-effects-and-processes)). Qapital builds saving rules on IF-THEN ([Wikipedia](https://en.wikipedia.org/wiki/Qapital)). | **The chapter-end "payday plan"**: the player picks one if-then plan in their own words from three options ("When pay lands, R___ goes to savings before I open any app"). Mali brings it back on Chapter 2 Day 1. This is the main **return hook**, and it replaces streaks. |
| B7 | **Mental accounting** (money is labelled, not fungible) | Thaler 1999† ([doi](https://doi.org/10.1002/(SICI)1099-0771(199909)12:3%3C183::AID-BDM318%3E3.0.CO;2-F)). Stokvels and grocery stokvels are culturally native labelled accounts (Rulashe 2025; Baloyi). | Keep **cash, savings, stokvel pot** visibly separate. The windfall scenario tests "found money" (it feels spendable). The reveal labels lines by bucket (Transport, Food, Family, Bills, Pay-later). Grocery stokvel money is *committed*: shown as "In the stokvel: R150", not as savings. |
| B8 | **Future self / goal gradient** | Hershfield et al. 2011† (age-progressed self raised saving) ([doi](https://doi.org/10.1509/jmkr.48.SPL.S23)), cited in `ijnrefm` | The player's goal (e.g. "R2,000 buffer") as a Rand bar with "R___ to go". Mali occasionally relates a choice to it in Rand ("That's R80 closer"), never with praise. |
| B9 | **Fresh-start effect** | Dai, Milkman & Riis 2014† (temporal landmarks drive new behaviour) ([doi](https://doi.org/10.1287/mnsc.2014.1901)) | Use **payday and new chapters** as landmarks instead of streaks. A missed day costs nothing. Each chapter and payday is a clean start. |
| B10 | **Character as teacher** | Mulcahy et al. 2020 (corpus): character → knowledge, strongest for casual users. Santos 2024: narrative drives immersion. | Mali is the delivery vehicle. Her lines must carry **memory** ("Yesterday the taxi went up R2. Today it's data."). That makes the day feel like a story, not a quiz. |
| B11 | **Peak-end rule** | Kahneman et al.† | The end of the day is the reveal, and the end of the chapter is the reflection. Make both calm, beautiful and unhurried. Players remember those moments most. |
| B12 | **Why streaks and leaderboards backfire here** | §1.3 | Not in the game. "Days played" is never shown. The HUD shows **Day N of 7** as story time, not a streak. Nothing is lost by skipping a real-world day. |

---

## 4. Comparable products and games

| Product | Market | What it does well | What it does badly (for our player) |
|---|---|---|---|
| **SPENT** (McKinney for Urban Ministries of Durham) ([playspent.org](https://playspent.org/); [Wikipedia](https://en.wikipedia.org/wiki/Spent_(video_game))) | US, web/mobile | 2M+ plays, average about 12 minutes. Every choice is between two bad options, so the trade-off is felt. Real cases told with compassion. | A one-shot empathy piece *about* poverty, not a rehearsal *for* your own month. US dollars. Guilt-heavy, and no reason to return. |
| **PAYBACK** (NGPF/McKinney) ([NGPF](https://www.ngpf.org/blog/paying-for-college/ngpf-launches-payback/)) | US | 30-minute decision game on student debt, with a reflection worksheet. | Single topic, US college system, played once. |
| **Bite Club** (Doorways to Dreams) ([RAND WR-963](https://www.rand.org/content/dam/rand/pubs/working_papers/2012/RAND_WR963.pdf)) | US | Metaphor game (a vampire club) that teaches saving versus debt versus spending. A pilot with 84 low-income adults raised knowledge and confidence. | The metaphor puts distance between the game and real life, and confidence is measured rather than behaviour. Dated. |
| **BitLife** ([Google Play](https://play.google.com/store/apps/details?id=com.candywriter.bitlife&hl=en_US), 50M+ downloads) | Global | Shows the appetite: text choices with life consequences, very replayable, funny. | Money is consequence-light and comedic. No reflection. Its 17+ "chaos" tone is the opposite of MaliGo's. |
| **Escape from the Castle** (Museum of Saving, Turin, in the corpus) | Italy | Short puzzles and co-op play. Money framed as a means, not an end. | For younger players, puzzle-based, no Rand reality. Leaderboards were used to drive replay. |
| **DreamScape / Where's the Finance** (Santos 2024, corpus) | Portugal | A narrative version and an idle version compared head-to-head with young adults. | Prototypes. Puzzles are quiz-like (a gargoyle asks questions), which breaks "knowledge ≠ behaviour". |
| **City of TMRW** (UOB digital bank, Thailand/Indonesia) (Lai & Langley 2024) | SE Asia | A city-building game where your real saving grows the city. Payday campaign: "Chasing your dreams starts on pay day". | Exists to retain the bank's customers. Rewards whoever already has a surplus. |
| **Ant Forest** (Alipay) | China | Very successful: real behaviour grows a virtual tree that becomes a real one. Collective and social. | Platform lock-in. Not about personal money resilience. |
| **YNAB** (Hayes MRP) | Global | "Turn every category green" is satisfying and gives clear immediate feedback. | Needs manual budgeting discipline. Users describe it as addictive. Paid, US-centric. |
| **Qapital** | US | IF-THEN saving rules: implementation intentions automated. | Needs a US bank account and a surplus to automate. |
| **Duolingo** | Global | The best-known streak habit loop. | Exactly the loss-aversion mechanic MaliGo rules out. Fine for verbs, harmful under money stress. |
| **Discovery Bank Vitality Money** ([Wikipedia](https://en.wikipedia.org/wiki/Discovery_Bank)) | SA | Status tiers reward real money behaviours with better rates (up to 10% on savings at top status, per search summary). | Behaviour-based but tiered: it rewards people who already have slack. Discovery customers only, skewed to higher incomes. |
| **GoTyme (formerly TymeBank) GoalSave** ([GoalSave](https://www.tymebank.co.za/save-earn/goalsave/)) | SA | Named goals, plus a commitment device: 6% p.a., or 10% p.a. if you never withdraw and give notice. Low-cost, mass-market. | It's a product, not a rehearsal. Doesn't help in the moment of a family call or a taxi hike. |
| **FNB eBucks** | SA | Points and levels for card and app behaviour. | Loyalty for the bank's own products, not resilience. |
| **Vault22 (formerly 22seven)** ([Google Play](https://play.google.com/store/apps/details?id=com.twentytwoseven.android&hl=en_US)) | SA | Aggregates every account. Published SA black-tax research. | Retrospective tracking: it shows what happened after the fact, not before. Some features moved behind a paywall. |
| **Stokvel accounts at major SA banks** ([UNSGSA case](https://www.unsgsa.org/stories/embedding-trust-stokvels-and-nedbank-build-culture-financial-health)) | SA | Formalise trust and interest for existing groups. | Assumes you're already in a stokvel. No help with the decision to join, or with scam fear. |

### 4.1 Where MaliGo's moat is

1. **Rehearsal before the moment, not tracking after it.** SA apps show you last month. MaliGo lets you live next
   week first. No SA product does this.
2. **SA push moments in Rand, at street level:** a R2 fare hike, an R85 data bundle, the gogo's call, a R500
   mashonisa loan with R750 to pay back, the 25th. International games can't localise this cheaply, and SA banks
   won't model a mashonisa or a black-tax call honestly.
3. **A non-judging companion with memory.** Mulcahy et al. show a character drives learning for casual users. Mali
   is the brand (React Native app artwork), and she is the only actor in this market who will say "Family matters
   too" *and* "That's R200 further from your buffer" in the same breath.
4. **No surplus required.** Bank gamification rewards people who already save. MaliGo is useful most exactly
   when you have R600 and seven days.
5. **Its weak points are honest:** low production values next to BitLife, and no real-money link. Don't fight on
   breadth. Win on specificity and warmth.

---

## 5. Proposed Chapter 1: "Seven days to the 25th"

### 5.1 Setup

- **The calendar:** Day 1 = the 18th (a Monday), Day 7 = the 24th (a Sunday). Payday is the 25th, the morning after the chapter
  ends. The HUD shows "Mon 18th · Day 1 of 7 · Payday in 7 days".
- **Same start for everyone in Chapter 1: cash R600, savings R400, a goal of a R2,000 buffer.** Right now the start
  depends on the life stage picked at character creation: R1,500 (Student) up to R25,000 cash (Financial Independence),
  `FinancialStats.cs:31-79`. With R15,000, a R50 lunch means nothing and the reveal loses its tension.
  Recommendation: keep the life-stage flavour for Mali's lines, but normalise Chapter 1 money. *(Needs a design
  decision. This is not a code change in this phase.)*
- **Income:** one casual shift a day, R150, 20 energy (existing, `WorkInteraction.cs`). Over 7 days that's up to R1,050.
- **Bills (existing):** airtime and data R60 settles on the night of Day 2, rent R500 on the night of Day 3
  (`ObligationDefaults.cs:15-25`). Bills are paid from cash only, and arrears add 10 stress a night.
- **Pacing:** 2 scenarios a day, plus the shift and sleep. That's 14 scenarios: the 8 existing plus 6 new.
  Each day has one "money in the world" moment and one "people" moment where possible.

### 5.2 Day-by-day

| Day | Theme (push) | Scenarios | Bill / carry-over that night |
|---|---|---|---|
| 1 Mon 18th | "Just another Monday": learn the loop | `food_decision`, `transport_decision` | Reveal introduces "Tomorrow: airtime R60" |
| 2 Tue 19th | Small leaks | **N1 `data_runs_out`**, `impulse_purchase` | Airtime and data R60 settles |
| 3 Wed 20th | Prices move | **N2 `taxi_fare_rise`**, `credit_bnpl` | Rent R500 settles. BNPL instalments start (see fix F1) |
| 4 Thu 21st | People you love | `family_obligation`, **N3 `group_chat_contribution`** | BNPL instalment 1 (with F1) |
| 5 Fri 22nd | The shock | `emergency_expense`, then **N4 `mashonisa_offer`** (best if gated to cash < R300, otherwise always offered) | – |
| 6 Sat 23rd | Community money | `stokvel_decision` (Saturday meeting), **N5 `grocery_stokvel`**, `windfall` (a side-hustle payment comes in) | BNPL instalment 2. Mashonisa repayment if taken (due Day 7 morning) |
| 7 Sun 24th | Tomorrow is the 25th | **N6 `debit_order_check`** | Night: **chapter-end reflection**, then the payday plan (if-then) |

Three scenarios on Day 6 is deliberate: Saturday has no shift pressure. If that's too long on a phone, move `windfall` to Day 4.

**Rough economy check** (my arithmetic on current values plus the new ones below, 7 shifts worked):
- *Tight path* (cheap meal, walk, walk away, cover the emergency from savings, partial family support, decline extras): ends with about **R1,000 cash, R0–R100 savings.**
- *Middle path:* ends with about **R250–R400 cash**.
- *Comfort path* (R50 meals, private rides, buy the impulse item, pay in full, full family support, dinner):
  runs out of cash around Day 5, and that's exactly when the mashonisa offer appears. This is the point:
  the comfort path isn't "wrong", it simply arrives at the 24th with a choice the tight path never faces.

Fine-tune these after playtesting. The numbers will move when the starting cash and F1–F3 are decided.

### 5.3 New scenarios (SA-specific gaps)

Every choice has `financialXpDelta = 5`. Tags use the existing enum (`Neutral | Frugal | Discretionary | Deferred`).
Rand deltas are kept simple so `ScenarioChoice` fields can express them. Where a scenario needs logic that doesn't
exist yet, it's flagged **[needs logic]**.

**N1 `data_runs_out`: "Out of data"** (location: Player_House, Tuesday)
Intro (Mali): "Your data just ran out, {0}. Group chat's busy and you've got a CV to send."
| Choice | Effect | Tag | Mali reflects |
|---|---|---|---|
| Buy 1GB for the month (R85) | Cash −85 | Neutral | "R85 and you're sorted till next month, {0}. That's most of today's shift." |
| Buy a day bundle (R15) | Cash −15, Stress +2 | Deferred | "R15 covers today, {0}. The same thing comes up again tomorrow." |
| Wait for free Wi-Fi at the mall | Energy −8, Stress +3 | Frugal | "No money out, {0}. It cost you a walk and a few hours of waiting." |

**N2 `taxi_fare_rise`: "Fare went up"** (location: Road_Main, Wednesday)
Intro: "Rank marshal says it's R17 now, {0}. Fuel went up again."
| Choice | Effect | Tag | Mali reflects |
|---|---|---|---|
| Pay the new fare both ways (R34) | Cash −34 | Neutral | "Two rands each way, {0}. Over a month of workdays that's about R80 more." |
| Join a colleague's lift club (R100 for the week) | Cash −100, Stress −3, Energy +5 | Discretionary | "R100 up front, {0}, and the week's travel is sorted. Less waiting at the rank too." |
| Walk to the main road, taxi from there (R17) | Cash −17, Energy −12 | Frugal | "You kept R17, {0}. Your legs paid the rest." |
(Source for the R2: Daily Maverick, May 2026. "About R80" = R2 × 2 × ~20 workdays.)

**N3 `group_chat_contribution`: "Birthday in the group chat"** (location: Local_Commercial_Hub, Thursday)
Intro: "Thandi's birthday dinner is Saturday, {0}. Everyone's putting in R250."
| Choice | Effect | Tag | Mali reflects |
|---|---|---|---|
| In for the dinner (R250) | Cash −250, Stress −6 | Discretionary | "Saturday's going to be a good one, {0}. That's R250 of the week gone, and payday is four days away." |
| Send R80 for the gift, skip the dinner | Cash −80, Stress +3 | Neutral | "You showed up for her, {0}, just not at the table. R170 stays with you." |
| "Next time, I promise" | Stress +5 | Frugal | "That message is hard to send, {0}. Your week stays as it was." |
(Peer influence is a named SA saving driver: Struwig et al. 2023.)

**N4 `mashonisa_offer`: "Till payday"** (location: Player_House, Friday, after the emergency)
Intro: "Bra K heard you're short, {0}. He'll give you R500 now, R750 back on the 25th. No forms."
| Choice | Effect | Tag | Mali reflects |
|---|---|---|---|
| Take R500 | Cash +500, Stress +6; 1 instalment of R750 due in 2 days | Deferred | "You've got breathing room till Sunday, {0}. On Sunday R750 goes back. That's R250 for two days." |
| Take R200 | Cash +200, Stress +3; 1 instalment of R300 due in 2 days | Deferred | "Smaller, {0}. R300 goes back Sunday, and R100 of it is the price of the loan." |
| Say no, look at what's left | Stress +4 | Neutral | "Let's look at what's left, {0}, and what's still due before the 25th." |
(Uses the existing `instalmentCount/Amount/IntervalDays` fields with interval 2, so it settles inside the chapter. The 50% rate matches the typical 30–50% range sourced above.)

**N5 `grocery_stokvel`: "December groceries"** (location: Local_Commercial_Hub, Saturday)
Intro: "Mam' Dlamini's grocery stokvel is taking members, {0}. R150 a month, and the bulk shop is in November."
| Choice | Effect | Tag | Mali reflects |
|---|---|---|---|
| Join (R150 a month) | Cash −150 now (to a "stokvel pot", not savings) **[needs logic: a separate bucket, or use savings with the label "Grocery stokvel"]** | Frugal | "That R150 is December's food, {0}. It's out of reach till November, and that's the idea." |
| Share a place with your cousin (R75 each) | Cash −75 | Neutral | "Half the cost, half the hamper, {0}. You'll need to trust each other to stay in." |
| Not this year | – | Neutral | "Fair, {0}. December will come either way, so it's worth thinking about what it'll need." |

**N6 `debit_order_check`: "It runs tomorrow"** (location: Player_House, Sunday)
Intro: "Your gym debit order goes off tomorrow, the 25th: R199, {0}. Pay lands the same day, sometimes after the debit order."
| Choice | Effect | Tag | Mali reflects |
|---|---|---|---|
| Move R199 from savings to cover it | Savings −199, Cash +199 then −199 (net savings −199) | Neutral | "It'll clear, {0}. Your buffer is R199 lighter until you top it up." |
| Cancel the gym, run in the park instead | Stress +2 | Frugal | "R199 a month back in your pocket from next month, {0}. Check the notice period." |
| Leave it and hope pay lands first | Stress +6. If cash < R199 at night: an R130 unpaid fee **[needs logic: a conditional fee]** | Deferred | "If it bounces, that's R130 on top, {0}. If it doesn't, nothing changes." |
(R130 = Standard Bank unpaid item fee. The 1.2m/month bounce figure is from PASA.)

**N7 `payday_first_hour`: Chapter 2 Day 1 opener (the hook)**
Intro: "It's the 25th, {0}. Pay's in. Last night you said: '{payday plan}'."
Choices: Do the plan (move R___ to savings) · Pay what's owed first · Payday lunch with the team (R180).
Mali reflects by referring back to the player's own sentence. This is the return mechanic (§3, B6).

**Optional N8 `bank_fee_notice`** (Chapter 2): "Why is my balance R57 less?" It explains the monthly fee plus an ATM
withdrawal fee. Choices: move to a cheaper account (Energy −10, one time), use the card instead of cash, leave it.

### 5.4 The end-of-day reveal (how it should read)

Show it after "Sleep – end the day" (`HomeInteraction.cs:106-113`), before the next morning. Mali's portrait is
on the left. No colours meaning good or bad, no ticks or crosses, no grade. Money in and money out are shown with
+/− signs only.

```
TUESDAY, 19TH

You started the day with R612.
You ended it with R542.

Here's what moved it
  + R150   Shift at work
  − R85    Data — 1GB for the month
  − R60    Airtime & data (bill)
  − R75    ...
  Savings: R400 → R400

Coming up
  Wed night  Rent  R500
  Payday in 6 days

Mali: "Data and airtime were most of today, Lindiwe.
       Rent comes off tomorrow night: R500. You've got R542."
```

Rules for the reveal:
1. **Lines in the order they happened** (the day as a story). A future "sort by size" toggle is fine, but chronological is the default.
2. **Every line is caused by something the player did, or a bill they knew about.** Nothing appears out of nowhere.
3. **Mali's line has three parts:** the biggest mover, in Rand, then what's next, in Rand, then *optionally* the goal in Rand.
   No adjective that evaluates ("great", "careful", "too much").
4. **Show "Coming up"** for the next 2 nights, so tomorrow is always an open question (the return hook without streaks).
5. **Data needed** (not built yet): the cash and savings snapshot at the start of the day, and a per-day list of
   `(label, delta, bucket)`. Nothing in `PlayerData` stores this today. The only day state is `currentDay`
   (`PlayerData.cs:20`) and `lastWorkedDay`.

### 5.5 The chapter-end reflection (night of Day 7)

```
SEVEN DAYS

On the 18th you had R600, and R400 put away.
Tonight you have R___, and R___ put away.

What moved it most
  Rent                R500
  The geyser          R600   (from savings)
  Thandi's dinner     R250

What Mali noticed
  "When something broke, you reached for savings first.
   Twice you waited before buying, and both times the
   thing was still there the next day."

Tomorrow is the 25th.
If you could decide one thing about payday tonight, what would it be?
  ○ When pay lands, R___ goes to savings before I open any app.
  ○ When pay lands, I pay what I owe first.
  ○ When pay lands, I set aside R___ for family before anything else.
```

- "What Mali noticed" is built from the hidden behaviour tags, phrased as **observations of actions**, never traits
  ("you reached for savings", never "you are a saver"). Choose 2 observations. Never show the scores.
- The if-then choice is stored and quoted back in N7. The blank is filled with a suggested Rand amount the player can adjust.
- There's no "chapter complete" fanfare and no star rating. A calm close (peak-end).

---

## 6. Changes this research suggests to existing content

### 6.1 Concrete issues found (read-only. These are for the spec and build phases)

| # | Where | Issue | Suggestion |
|---|---|---|---|
| F1 | `ScenarioLibrary.cs:416-418` | BNPL instalments are 3 × R120 every **7 days**. If BNPL is chosen on Day 3, they fall due on Days 10/17/24, **all after a 7-day chapter**, so the player never feels the consequence inside the chapter. | Set the interval to 1–2 days for Chapter 1 ("every 2 days"), or move BNPL to Day 1. Either way, at least two instalments should land before the reflection. |
| F2 | `ScenarioManager.cs:114-115` | `cash = Max(0, cash + delta)` and the same for savings. Covering R600 from R400 savings costs only R400, and an R400 purchase with R250 cash is free beyond R250. The choice quietly becomes cheaper. | Either disable unaffordable choices with a reason ("You have R250"), or route the shortfall to arrears like bills. Never silently discount it. |
| F3 | `FinancialStats.cs:31-79` + `CharacterCreationUI.cs:396` | Starting cash ranges from R1,500 to R25,000 depending on the life stage chosen, which flattens every Chapter 1 choice for the higher stages. | Same Chapter 1 start for all (R600 cash / R400 savings, §5.1). |
| F4 | `ScenarioLibrary.cs:193` | "That's the move, {0}. Turned a want into progress…" is praise, so it marks a correct answer. | "R120 into savings, {0}. That's R120 closer to your buffer." |
| F5 | `ScenarioLibrary.cs:266` | "Straight to the goal, {0}. Nice." "Nice" is praise. | "All R300 to savings, {0}. Your buffer's at R___ now." |
| F6 | `ScenarioLibrary.cs:379` (+ decline label `:375`) | "Better to sit this one out than regret it" and "not worth the risk" frame stokvels as risky. SA evidence says stokvel members save more, and that the real fear is scams. This is judgmental and culturally off. | Label: "Not this one, for now". Mali: "It's your money and your trust, {0}. If you join one later, knowing who holds the money matters most." |
| F7 | `ScenarioLibrary.cs:429` | "Sometimes walking away is the whole trick" implies a correct answer. | "Nothing spent, {0}. Your month stays as it was." |
| F8 | `ScenarioLibrary.cs:355-358` | Joining the stokvel moves R100 from cash straight into `savings`, which is liquid. A rotating stokvel pot isn't withdrawable at will. | Show it as a committed "stokvel pot" (same need as N5). |
| F9 | `ScenarioLibrary.cs:272` | "Treat yourself" adds **Cash +R300**, the same total as banking it, so the only difference is which bucket. That's fine for mental accounting, but say it: Mali can name the bucket. | "R300 in your pocket, {0}. Spending money now, not buffer." |
| F10 | `ScenarioWorldWiring.cs:28-63` | All 8 triggers are live at once, and nothing gates a scenario to a day. | Chapter 1 needs a day → scenario schedule (§5.2) as data, switchable for A/B (MASTER_PROMPT §5). |

### 6.2 Tone guide for Mali (from the research)

- **Say what happened, in Rand. Say what's next, in Rand. Stop.**
- Name a value the player holds ("family matters too") without ranking it against saving.
- Use memory: refer back to yesterday's choice at least once a day.
- Never: "good", "nice", "smart", "careful", "should", "mistake", "well done", and no exclamation-mark praise.
- Grown-up warmth: short sentences, the player's name, SA register without caricature ("the rank", "the 25th",
  "gogo", "Bra K") and no slang overload.

---

## 7. Later content packs

Notes on future content packs are kept privately by the founder.

---

## 8. What I could not verify

- **OMSIM 2026 figures** come from two press write-ups (FA News, BusinessDay). I didn't open the full Old Mutual
  report PDF. The "Gen Z 18–29 working" base is as the articles describe it.
- **"76% run out before month-end"** comes from sponsored content by a lending app (Floatpays, 2023). The Standard
  Bank "almost half under R1,000 by payday" figure (2024) is stronger.
- **Black tax 68%** is from a vendor survey (Vault22) reported in the press. Its method wasn't checked.
- **NSFAS amounts** come from student-information sites, not NSFAS's own published guide.
- **Standard Bank R130 unpaid-debit-order fee** and **Discovery "up to 10%"** come from search-result summaries. I didn't open the pricing PDFs.
- **Mashonisa rates and counts** (30–50% typical, 40–50k lenders) are from press and blog sources. There's no
  official dataset, because the trade is unregulated.
- **The R15 day-data bundle** in N1 is an in-game approximation, not a quoted tariff.
- **Principles marked †** (COM-B, Fogg, Thaler, Laibson, Hershfield, Karlan et al., Dai et al., Fernandes et al.,
  peak-end) are cited from established literature I know. I didn't re-fetch them this session. The DOIs given are the standard ones.
- **The Celestin & Vanitha "25% confidence" figure** is from a weak-method conference paper (unclear sample).
  It's the corpus's only direct source for "false confidence", so the spec shouldn't lean on it alone.
- **The economy check in §5.2** is my hand arithmetic. It wasn't run as a scripted check. Phase 3 should add a
  script (MASTER_PROMPT §7) that plays the tight, middle and comfort paths through 7 days.
- **How it feels to play** (pacing of 2 scenarios a day, reveal length on a landscape phone) needs a person with a phone.
