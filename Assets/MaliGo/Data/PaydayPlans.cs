using System;

namespace MaliGo.Data
{
    public struct PaydayPlan
    {
        public string id;
        public string text;
    }

    /// <summary>The payday plans offered at the chapter end (design spec 4.5).</summary>
    public static class PaydayPlans
    {
        public const string SaveFirstId = "save_first";
        public const string OwedFirstId = "owed_first";
        public const string FamilySetId = "family_set";
        public const string WaitADayId = "wait_a_day";

        public static readonly PaydayPlan[] All =
        {
            new PaydayPlan { id = SaveFirstId, text = "When pay lands, R200 goes into savings before I buy anything." },
            new PaydayPlan { id = OwedFirstId, text = "When pay lands, I pay what I owe first, then everything else." },
            new PaydayPlan { id = FamilySetId, text = "When pay lands, I set aside one amount for family, and stick to it." },
            new PaydayPlan { id = WaitADayId, text = "When pay lands, I wait one day before any shopping." }
        };

        /// <summary>The plan with this id; an unknown id gives default(PaydayPlan) (empty id).</summary>
        public static PaydayPlan Get(string id)
        {
            foreach (PaydayPlan plan in All)
            {
                if (string.Equals(plan.id, id, StringComparison.Ordinal))
                {
                    return plan;
                }
            }

            return default(PaydayPlan);
        }
    }
}
