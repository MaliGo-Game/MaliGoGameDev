# Bank feed: the week built from real spending

Status: built against sample data behind a provider seam. Off in the beta (`MaliGoFeatures.BankFeedOnboarding = false`).
There is no live connection and no network code in the game.

## What it does

A player can choose to connect their bank during onboarding. The phone reads their recent transactions, sorts them
into spending habits (eating out, taxis, data, groceries...), and uses those habits to pre-fill the two existing
"spending profile" taps (where the money goes, how they get around). Those two values already decide which of the
four weekly schedules runs, which scenario variants appear (for example the kota run for food players, the walking
variants for walkers) and what the places are called. So someone who buys kotas most days gets the food week and the
kota shop; someone who commutes by minibus taxi gets the transport week and the taxi rank.

Skipping keeps the two taps exactly as they were before this feature.

## Code map

| File | Role |
| --- | --- |
| `Assets/Scripts/BankFeed/ObModels.cs` | Open Banking Account Information data model (accounts, transactions, permissions, endpoints). In memory only. |
| `Assets/Scripts/BankFeed/MiniJson.cs`, `ObJsonParser.cs` | Reader for the OB JSON envelope (`Data.Transaction[]`, `Data.Account[]`). No reflection, IL2CPP safe. |
| `Assets/Scripts/BankFeed/ITransactionSource.cs` | The provider seam: `ITransactionSource` and the `TransactionSources` registration hook. |
| `Assets/Scripts/BankFeed/SampleTransactionSource.cs` | Five made-up personas, 60 days each, seeded and fixed-date (deterministic). |
| `Assets/Scripts/BankFeed/TransactionCategoriser.cs` | MCC first, then keywords, then "other". Credits are income unless they read as a refund. |
| `Assets/Scripts/BankFeed/HabitSummaryBuilder.cs` | Transactions to `BankHabitSummary` (shares, rates, typical amounts, payday, flags). |
| `Assets/Scripts/BankFeed/BankFeedMapping.cs` | Summary to `SpendingFocus` / `TravelMode`, plus a weight per scenario spot. |
| `Assets/Scripts/BankFeed/BankFeedSession.cs` | Runs consent, accounts, transactions, summary; drops the raw list; disconnects. |
| `Assets/Scripts/BankFeed/BankFeedCopy.cs` | All player-facing words (plain, warm, generic). |
| `Assets/MaliGo/Data/BankHabitSummary.cs` | The only thing saved (`PlayerData.bankHabits`). |
| `Assets/Scripts/PlayerIdentity/CharacterCreationUI.BankFeed.cs` | The optional onboarding screen. |
| `Assets/Scripts/UI/PauseMenuView.cs` | "Forget my bank data". |
| `tools/logic_tests/Tests/BankFeedTests.cs` | Parser, categoriser, summary, mapping, session, privacy and copy tests. |

Everything in `Assets/Scripts/BankFeed/` is pure C# (System only) and runs in the logic tests.

## Data model (Open Banking Account Information)

The flow mirrors the standard: **consent, then accounts, then transactions**.

Permissions requested (and nothing more): `ReadAccountsBasic`, `ReadTransactionsBasic`, `ReadTransactionsDetail`,
`ReadTransactionsCredits`, `ReadTransactionsDebits`.

Endpoints a provider serves: `GET /accounts`, `GET /accounts/{AccountId}/transactions`, `GET /transactions`.

Transaction fields used:

| Field | Notes |
| --- | --- |
| `TransactionId`, `AccountId` | strings |
| `Amount.Amount`, `Amount.Currency` | decimal string ("45.50"), "ZAR"; a bare JSON number is accepted too |
| `CreditDebitIndicator` | `Credit` / `Debit` |
| `Status` | `Booked` / `Pending` (only booked transactions count) |
| `BookingDateTime` | ISO 8601 with offset |
| `TransactionInformation` | free text, used only for keyword rules |
| `MerchantDetails.MerchantName`, `MerchantDetails.MerchantCategoryCode` | ISO 18245 MCC |
| `BankTransactionCode.Code` / `SubCode` | optional |

Envelope: `{ "Data": { "Transaction": [ ... ] }, "Links": { ... }, "Meta": { ... } }`. Unknown fields are ignored.

## Categories

MCC first:

| Category | MCCs |
| --- | --- |
| `eating_out` | 5812, 5814 |
| `transport` | 4111, 4112, 4121, 4131 (4121 with ride-app wording becomes `ehailing`) |
| `data_airtime` | 4812, 4814, 4816 |
| `groceries` | 5411, 5422, 5441, 5451, 5462, 5499 |
| `fuel` | 5541, 5542, 5983 |
| `clothing` | 5311, 5611, 5621, 5651, 5661, 5691, 5699 |
| `entertainment` | 4899, 5813, 7832, 7841, 7922, 7994, 7995, 7997, 7999 |
| `housing` | 4900, 6513 |
| `cash` | 6010, 6011 |
| (keywords decide) | 4829, 6012, 6051 (transfers and financial institutions) |

Then keyword rules on merchant name + transaction information, in order: savings (stokvel, savings), loan_repayment
(loan, repayment, instalment...), family (money home, send money...), housing (rent, res fee, electricity...),
ehailing, eating_out (kota, takeaway, chips...), groceries (spaza, grocer...), transport (taxi, rank, bus, train,
fare), fuel, data_airtime, clothing, entertainment, cash, fees. Then `other`.

Credits are income (`salary`, `allowance`, `bursary`, `grant`, else `irregular`) unless they read as a refund or reversal.

## Habit summary (what is saved)

`BankHabitSummary`, per category: share of spend, payments per week, typical (median) amount. Plus: payday day of
month (two or more main income credits within three days of the month of each other; month ends wrap), income kind,
cash withdrawals per week and typical size, and flags:

| Flag | Rule |
| --- | --- |
| EatsOutOften | eating out 3 or more times a week |
| TaxiCommuter | 4 or more fares a week |
| DataHeavy | data/airtime 2 or more times a week, or 12% or more of spend |
| CashUser | 1 or more cash withdrawals a week, or 20% or more of spend |
| SendsMoneyHome | any money-home transfer |
| HasLoanRepayments | any loan or store-account repayment |

No transaction, merchant name, account id or single payment amount is in it; strings are fixed ids only (a test checks).

## From habits to the game

Integration is through the inputs the game already has, so the schedule and scenarios are unchanged:

**(a) Spending profile.** Each day-to-day category (eating out, groceries, fares, rides, fuel, data, clothing,
entertainment, money home) scores half its share of spend and half its share of payments. Rent, loans and savings
are left out: everyone has them, they are not the habit the week is built around.

* food = eating out
* transport = fares + rides + fuel
* data_social = data/airtime + entertainment + half clothing
* home_family = money home + half groceries

Travel: walk when under 1.5 fares/rides a week and almost no fuel; otherwise the largest of fares (taxi), rides
(e-hailing) and fuel stops x4 (car).

The onboarding screen pre-fills the two taps with these; the player can change them; `SpendingProfiles.SetFromOnboarding`
runs as before.

**(b) Spot weights.** `BankFeedMapping.SpotWeights` gives each of the six spots a weight (sums to 1): CORNER = eating out
+ half data + half groceries; TAXI = fares + rides + fuel; HUB = savings + half loans + half cash; SHOPFRONT = clothing
+ half entertainment; GATE = money home + half rent/utilities + half groceries; EAST = half loans + half cash + half
entertainment. It is ready for schedule tuning; the schedule itself does not read it yet.

Sample personas and where they land:

| Persona | Focus | Travel | Notes |
| --- | --- | --- | --- |
| Student living in res | food | taxi | kota most days, bursary on the 25th |
| Shop assistant who commutes | transport | taxi | taxi commuter, money home, stokvel, salary on the 25th |
| Freelance content creator | data_social | ehailing | data heavy, irregular income |
| Care worker supporting family | home_family | walk | cash user, money home, loan, salary on the 15th |
| Office worker with a car | transport | car | store account, salary on the 28th |

## Privacy and consent (POPIA)

* **Opt-in only.** The screen exists only when the flag is on, and it is skippable at every stage. Skip clears any summary.
* **Plain-language consent** before anything is read: what is read (accounts, 60 days of money in and out), that only
  habits stay on the phone, that payments are never saved, that nothing is sent anywhere, and that it can be skipped
  and forgotten later.
* **On the phone only.** Transactions are reduced to a summary inside `BankFeedSession` and the list is cleared at once.
  Raw transactions are never written to disk, logged or sent. The game has no network code.
* **Only the summary is saved** (`PlayerData.bankHabits`).
* **Forget my bank data** in Pause empties the summary and saves twice, so the backup copy of the save does not keep
  it either. Start over deletes everything.
* **Sample data is labelled** "Sample data, not yours" with the persona's description. Without a live provider the
  screen says plainly that a live connection is not available yet.

## Save format

`PlayerData.bankHabits` is a new field with an initialiser. JsonUtility fills a missing field from the initialiser,
so an old save loads with an empty summary. `PlayerData.CurrentSaveVersion` stays 2 (a bump would discard old saves).
`PlayerDataManager.Repair` and `ChapterFlow.StartChapter` never leave it null; "Live the week again" keeps it.
`BankHabitSummary.version` versions the shape on its own.

## The provider seam

```csharp
public interface ITransactionSource
{
    string SourceId { get; }
    bool IsSample { get; }
    void RequestConsent(ObConsentRequest request, Action<ConsentResult> done);
    void GetAccounts(Action<AccountsResult> done);
    void GetTransactions(string accountId, DateTimeOffset from, DateTimeOffset to, Action<TransactionsResult> done);
    void Disconnect();
}
```

Plain callbacks, each called exactly once, now or later on the main thread (no async/await or reflection, so it is
the same under IL2CPP). A live adapter is one file that implements the interface and registers itself:

```csharp
// Assets/Scripts/BankFeed/Private/LiveTransactionSource.cs  (git-ignored, not in this repo)
static class LiveTransactionSourceRegistration
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => MaliGo.BankFeed.TransactionSources.RegisterLive(() => new LiveTransactionSource());
}
```

`Assets/Scripts/BankFeed/Private/` is in `.gitignore` and excluded from the logic tests; the public build compiles
without it. With an adapter registered, the consent stage offers "I agree, connect" instead of the sample person.
The adapter can use `ObJsonParser` on the provider's responses.

## What a real provider needs later

* **A licensed Account Information Service Provider (AISP) or a partner arrangement** with a data provider, covering
  the permissions above, and a POPIA review of the consent text and data flow.
* **An OAuth consent redirect flow.** The player is sent to the provider's own consent and login page (in the system
  browser or an in-app browser tab) and comes back through a deep link with an authorisation code. The game never
  sees bank credentials.
* **A small backend for the token exchange.** Client secrets and signing keys cannot live in the app, so a minimal
  server swaps the authorisation code for an access token (and ideally proxies the account and transaction calls).
  It should pass transactions straight through without storing them.
* **Categorisation can stay on the phone.** The backend only relays; the app reduces transactions to the summary as
  it does with sample data.
* **Token hygiene.** Short-lived tokens, `Disconnect()` revokes or drops them after the summary is made, and consent
  is re-asked for a new summary.

## Verifying on a phone

The logic is covered by `tools/logic_tests`. With the flag on, check on a device: the screen in portrait and
landscape (text fits, buttons clear of each other), Back through the stages, Skip leaving the taps empty, the
pre-filled taps on the profile screen, "Another sample person", and Pause showing "Forget my bank data" only while
a summary is saved, then hiding it after use.
