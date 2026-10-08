using System;
using System.Collections.Generic;

namespace MaliGo.BankFeed
{
    /// <summary>
    /// Where transactions come from (docs/BANK_FEED.md). Mirrors the Open Banking Account Information flow: consent,
    /// then accounts, then each account's transactions. Plain callbacks (no async/await, no reflection), so it works
    /// the same under IL2CPP. A source may call back at once (the sample source does) or later on the main thread
    /// (a live adapter). Every callback is called exactly once.
    ///
    /// The public repo ships only <see cref="SampleTransactionSource"/>. A live adapter implements this interface in
    /// its own file (kept out of this repo, e.g. in the git-ignored Assets/Scripts/BankFeed/Private/ folder) and
    /// registers itself with <see cref="TransactionSources.RegisterLive"/>; no public file changes.
    /// </summary>
    public interface ITransactionSource
    {
        /// <summary>A short generic id for logs and the save ("sample" or the adapter's own id). Never a brand name in this repo.</summary>
        string SourceId { get; }

        /// <summary>True for made-up data; the UI then labels everything as sample data.</summary>
        bool IsSample { get; }

        /// <summary>Asks the player (through the provider's own consent page, for a live source) for the permissions.</summary>
        void RequestConsent(ObConsentRequest request, Action<ConsentResult> done);

        /// <summary>GET /accounts.</summary>
        void GetAccounts(Action<AccountsResult> done);

        /// <summary>GET /accounts/{AccountId}/transactions for the consented window.</summary>
        void GetTransactions(string accountId, DateTimeOffset from, DateTimeOffset to, Action<TransactionsResult> done);

        /// <summary>Drops any token or session the source holds (called when the summary is made or the flow ends).</summary>
        void Disconnect();
    }

    public class ConsentResult
    {
        public bool Granted;

        /// <summary>A plain, player-safe reason when not granted ("cancelled", "unavailable"); never server text.</summary>
        public string Error = "";
    }

    public class AccountsResult
    {
        public bool Ok;
        public List<ObAccount> Accounts = new List<ObAccount>();
        public string Error = "";
    }

    public class TransactionsResult
    {
        public bool Ok;
        public List<ObTransaction> Transactions = new List<ObTransaction>();
        public string Error = "";
    }

    /// <summary>
    /// The registration hook. A live adapter outside the public repo registers itself early, e.g. from a
    /// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]</c> method in its own file.
    /// Without one, <see cref="HasLive"/> is false and the UI offers only clearly labelled sample data; nothing in
    /// the game pretends to connect.
    /// </summary>
    public static class TransactionSources
    {
        static Func<ITransactionSource> liveFactory;

        public static bool HasLive => liveFactory != null;

        /// <summary>Registers the live adapter (a factory, so each connection gets a fresh source). Last call wins.</summary>
        public static void RegisterLive(Func<ITransactionSource> factory)
        {
            liveFactory = factory;
        }

        /// <summary>A new live source, or null when none is registered.</summary>
        public static ITransactionSource CreateLive()
        {
            return liveFactory?.Invoke();
        }

        /// <summary>A sample source for a persona id (unknown ids give the first persona).</summary>
        public static ITransactionSource CreateSample(string personaId)
        {
            return new SampleTransactionSource(personaId);
        }

        /// <summary>For tests: forget the registered adapter.</summary>
        public static void ClearLive()
        {
            liveFactory = null;
        }
    }
}
