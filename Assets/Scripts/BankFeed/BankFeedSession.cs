using System;
using System.Collections.Generic;
using MaliGo.Data;

namespace MaliGo.BankFeed
{
    /// <summary>What one run of the bank feed gives back: a summary, or a plain error.</summary>
    public class BankFeedOutcome
    {
        public bool Ok;

        /// <summary>Set when Ok; the only thing that leaves the session.</summary>
        public BankHabitSummary Summary;

        /// <summary>"declined", "no_accounts", "no_transactions" or "failed" when not Ok.</summary>
        public string Error = "";
    }

    /// <summary>
    /// Runs the Open Banking flow against a source: consent -> GET /accounts -> GET /accounts/{AccountId}/transactions
    /// for each account -> <see cref="HabitSummaryBuilder"/>. The raw transactions exist only inside this run: they are
    /// reduced to a summary as soon as the last account answers, the list is cleared, and the source is told to
    /// disconnect. Nothing is written, logged or sent from here. Pure; works with a source that answers at once or
    /// later.
    /// </summary>
    public static class BankFeedSession
    {
        public const int DefaultWindowDays = 60;

        /// <summary>
        /// Runs the flow for the <paramref name="windowDays"/> days ending at <paramref name="until"/> and calls
        /// <paramref name="done"/> exactly once. Source, persona and creation time are filled into the summary.
        /// </summary>
        public static void Run(ITransactionSource source, DateTimeOffset until, int windowDays, long nowUnixSeconds,
            string samplePersonaId, Action<BankFeedOutcome> done)
        {
            if (source == null)
            {
                done?.Invoke(new BankFeedOutcome { Error = "failed" });
                return;
            }

            windowDays = Math.Max(7, windowDays);
            DateTimeOffset to = until;
            DateTimeOffset from = until.AddDays(-windowDays);
            var request = new ObConsentRequest { TransactionFromDateTime = from, TransactionToDateTime = to };
            bool finished = false;

            void Finish(BankFeedOutcome outcome)
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                source.Disconnect();
                done?.Invoke(outcome);
            }

            source.RequestConsent(request, consent =>
            {
                if (consent == null || !consent.Granted)
                {
                    Finish(new BankFeedOutcome { Error = "declined" });
                    return;
                }

                source.GetAccounts(accounts =>
                {
                    if (accounts == null || !accounts.Ok || accounts.Accounts == null || accounts.Accounts.Count == 0)
                    {
                        Finish(new BankFeedOutcome { Error = accounts != null && accounts.Ok ? "no_accounts" : "failed" });
                        return;
                    }

                    var gathered = new List<ObTransaction>();
                    int waiting = accounts.Accounts.Count;
                    int answered = 0;
                    foreach (ObAccount account in accounts.Accounts)
                    {
                        source.GetTransactions(account?.AccountId ?? "", from, to, page =>
                        {
                            if (page != null && page.Ok && page.Transactions != null)
                            {
                                gathered.AddRange(page.Transactions);
                                answered++;
                            }

                            waiting--;
                            if (waiting > 0)
                            {
                                return;
                            }

                            if (answered == 0)
                            {
                                gathered.Clear();
                                Finish(new BankFeedOutcome { Error = "failed" });
                                return;
                            }

                            BankHabitSummary summary = HabitSummaryBuilder.Build(gathered, windowDays);
                            gathered.Clear();
                            if (summary.categories.Length == 0)
                            {
                                Finish(new BankFeedOutcome { Error = "no_transactions" });
                                return;
                            }

                            summary.source = source.IsSample ? BankHabitSource.Sample : BankHabitSource.Live;
                            summary.samplePersonaId = source.IsSample ? samplePersonaId ?? "" : "";
                            summary.createdUnixSeconds = nowUnixSeconds;
                            Finish(new BankFeedOutcome { Ok = true, Summary = summary });
                        });
                    }
                });
            });
        }

        /// <summary>The sample flow for a persona, over its fixed 60-day window.</summary>
        public static void RunSample(string personaId, long nowUnixSeconds, Action<BankFeedOutcome> done)
        {
            string id = SampleTransactionSource.Persona(personaId).id;
            Run(new SampleTransactionSource(id), SampleTransactionSource.EndDate.AddDays(1), SampleTransactionSource.WindowDays,
                nowUnixSeconds, id, done);
        }
    }
}
