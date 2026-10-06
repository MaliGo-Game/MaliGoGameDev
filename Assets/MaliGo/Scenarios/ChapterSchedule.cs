using System;
using System.Collections.Generic;
using System.Globalization;
using MaliGo.Data;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// Chapter 1's day schedule, spots, place names, morning lines and teasers (design spec 3.1, 3.3).
    /// Pure. The player's spending focus picks one of four schedules; the profile values and the
    /// follow-up keys come in as plain arguments (data.spendingProfile.focus / .travel, data.followUps).
    /// Unknown focus/travel values are treated as "food"/"taxi".
    /// </summary>
    public static class ChapterSchedule
    {
        public const int ChapterLength = 7;
        public const int PaydayDay = 8;

        // Spending focus and travel ids (written out here so this file needs no other package's types).
        public const string FocusFood = "food";
        public const string FocusTransport = "transport";
        public const string FocusDataSocial = "data_social";
        public const string FocusHomeFamily = "home_family";
        public const string TravelTaxi = "taxi";
        public const string TravelEHailing = "ehailing";
        public const string TravelWalk = "walk";
        public const string TravelCar = "car";

        // Scenario spots (design spec 3.3).
        public const string SpotCorner = "CORNER";
        public const string SpotTaxi = "TAXI";
        public const string SpotHub = "HUB";
        public const string SpotShopfront = "SHOPFRONT";
        public const string SpotGate = "GATE";
        public const string SpotEast = "EAST";

        public static readonly string[] AllSpots = { SpotCorner, SpotTaxi, SpotHub, SpotShopfront, SpotGate, SpotEast };
        public static readonly string[] AllFoci = { FocusFood, FocusTransport, FocusDataSocial, FocusHomeFamily };
        public static readonly string[] AllTravelModes = { TravelTaxi, TravelEHailing, TravelWalk, TravelCar };

        public const string FollowUpTeaserText = "Tomorrow: your aunt calls back.";
        public const string PaydayTeaserText = "Tomorrow is payday.";

        // Day 5 of the other foci (the food focus adds its kota run to it, Revision 4).
        static readonly string[] GeyserAndBraK = { ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.MashonisaOfferId };

        static readonly string[][] FoodSchedule =
        {
            new[] { ScenarioLibrary.FoodDecisionId, ScenarioLibrary.TransportDecisionId },
            new[] { ScenarioLibrary.DataRunsOutId, ScenarioLibrary.CreditBnplId },
            new[] { ScenarioLibrary.TaxiFareRiseId, ScenarioLibrary.ImpulsePurchaseId },
            new[] { ScenarioLibrary.FamilyObligationId, ScenarioLibrary.GroupChatContributionId },
            new[] { ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.MashonisaOfferId, ScenarioLibrary.KotaRunId },
            new[] { ScenarioLibrary.StokvelDecisionId, ScenarioLibrary.WindfallId },
            new[] { ScenarioLibrary.DebitOrderCheckId },
        };

        static readonly string[][] TransportSchedule =
        {
            new[] { ScenarioLibrary.TransportDecisionId, ScenarioLibrary.FoodDecisionId },
            FoodSchedule[1], FoodSchedule[2], FoodSchedule[3], GeyserAndBraK, FoodSchedule[5], FoodSchedule[6],
        };

        static readonly string[][] DataSocialSchedule =
        {
            new[] { ScenarioLibrary.FoodDecisionId, ScenarioLibrary.GroupChatContributionId },
            FoodSchedule[1], FoodSchedule[2],
            new[] { ScenarioLibrary.FamilyObligationId, ScenarioLibrary.TransportDecisionId },
            GeyserAndBraK, FoodSchedule[5], FoodSchedule[6],
        };

        static readonly string[][] HomeFamilySchedule =
        {
            new[] { ScenarioLibrary.FoodDecisionId, ScenarioLibrary.FamilyObligationId },
            FoodSchedule[1], FoodSchedule[2],
            new[] { ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.GroupChatContributionId },
            new[] { ScenarioLibrary.TransportDecisionId, ScenarioLibrary.MashonisaOfferId },
            FoodSchedule[5], FoodSchedule[6],
        };

        static readonly string[] FollowUpIds = { ScenarioLibrary.FamilyCallbackId, ScenarioLibrary.FamilyCallbackFullId };

        // Morning lines and teasers, keyed by a day's scenario list (shared by every focus with that list).
        static readonly Dictionary<string, string> MorningLines = new Dictionary<string, string>
        {
            { Key(ScenarioLibrary.FoodDecisionId, ScenarioLibrary.TransportDecisionId), "Day 1, {name}. Lunch first, then the shift. A trip across town too." },
            { Key(ScenarioLibrary.TransportDecisionId, ScenarioLibrary.FoodDecisionId), "Day 1, {name}. A trip across town first, then the shift, and lunch." },
            { Key(ScenarioLibrary.FoodDecisionId, ScenarioLibrary.GroupChatContributionId), "Day 1, {name}. Lunch first, then the shift. The group chat's busy." },
            { Key(ScenarioLibrary.FoodDecisionId, ScenarioLibrary.FamilyObligationId), "Day 1, {name}. Lunch first, then the shift. Your phone will ring." },
            { Key(ScenarioLibrary.DataRunsOutId, ScenarioLibrary.CreditBnplId), "Day 2. Your phone's out of data, and the phone shop has a deal on." },
            { Key(ScenarioLibrary.TaxiFareRiseId, ScenarioLibrary.ImpulsePurchaseId), "Day 3. Getting to town costs more from today, and that hoodie's still there." },
            { Key(ScenarioLibrary.FamilyObligationId, ScenarioLibrary.GroupChatContributionId), "Day 4. Your phone's going to ring today, and the group chat is busy." },
            { Key(ScenarioLibrary.FamilyObligationId, ScenarioLibrary.TransportDecisionId), "Day 4. Your phone's going to ring today, and there's a trip across town." },
            { Key(ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.GroupChatContributionId), "Day 4. Something's not right at home, and the group chat is busy." },
            { Key(ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.MashonisaOfferId), "Day 5. Something's not right at home this morning." },
            { Key(ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.MashonisaOfferId, ScenarioLibrary.KotaRunId), "Day 5. Something's not right at home, and your friends want a kota run." },
            { Key(ScenarioLibrary.TransportDecisionId, ScenarioLibrary.MashonisaOfferId), "Day 5. There's a trip across town, and Bra K wants a word." },
            { Key(ScenarioLibrary.StokvelDecisionId, ScenarioLibrary.WindfallId), "Day 6. The stokvel meets at the shops today, and your neighbour's looking for you." },
            { Key(ScenarioLibrary.DebitOrderCheckId), "Day 7. Last day before payday." },
        };

        static readonly Dictionary<string, string> Teasers = new Dictionary<string, string>
        {
            { Key(ScenarioLibrary.DataRunsOutId, ScenarioLibrary.CreditBnplId), "Tomorrow: no data, and a deal at the phone shop." },
            { Key(ScenarioLibrary.TaxiFareRiseId, ScenarioLibrary.ImpulsePurchaseId), "Tomorrow: town costs more, and that hoodie again." },
            { Key(ScenarioLibrary.FamilyObligationId, ScenarioLibrary.GroupChatContributionId), "Tomorrow: a call from home, and a birthday." },
            { Key(ScenarioLibrary.FamilyObligationId, ScenarioLibrary.TransportDecisionId), "Tomorrow: a call from home, and a trip across town." },
            { Key(ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.GroupChatContributionId), "Tomorrow: something at home, and the group chat." },
            { Key(ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.MashonisaOfferId), "Tomorrow: something at home needs fixing." },
            { Key(ScenarioLibrary.EmergencyExpenseId, ScenarioLibrary.MashonisaOfferId, ScenarioLibrary.KotaRunId), "Tomorrow: something at home, and a kota run." },
            { Key(ScenarioLibrary.TransportDecisionId, ScenarioLibrary.MashonisaOfferId), "Tomorrow: a trip across town, and Bra K wants a word." },
            { Key(ScenarioLibrary.StokvelDecisionId, ScenarioLibrary.WindfallId), "Tomorrow: the stokvel, and your neighbour." },
            { Key(ScenarioLibrary.DebitOrderCheckId), "Tomorrow: the last day before payday." },
        };

        static string Key(params string[] ids) => string.Join(",", ids);

        // ------------------------------------------------------------------ profile ids

        /// <summary>Unknown or empty focus -> "food" (the same rule as SpendingFocus.Normalize).</summary>
        public static string NormalizeFocus(string focus)
        {
            switch (focus)
            {
                case FocusFood:
                case FocusTransport:
                case FocusDataSocial:
                case FocusHomeFamily:
                    return focus;
                default:
                    return FocusFood;
            }
        }

        /// <summary>Unknown or empty travel mode -> "taxi" (the same rule as TravelMode.Normalize).</summary>
        public static string NormalizeTravel(string travel)
        {
            switch (travel)
            {
                case TravelTaxi:
                case TravelEHailing:
                case TravelWalk:
                case TravelCar:
                    return travel;
                default:
                    return TravelTaxi;
            }
        }

        static string[][] ScheduleFor(string focus)
        {
            switch (NormalizeFocus(focus))
            {
                case FocusTransport: return TransportSchedule;
                case FocusDataSocial: return DataSocialSchedule;
                case FocusHomeFamily: return HomeFamilySchedule;
                default: return FoodSchedule;
            }
        }

        // ------------------------------------------------------------------ schedule

        /// <summary>The scenarios scheduled for a day (in order; the first is the day's gate). Empty outside Days 1-7.</summary>
        public static IReadOnlyList<string> ScenariosForDay(int day, string focus)
        {
            if (day < 1 || day > ChapterLength)
            {
                return Array.Empty<string>();
            }

            return Array.AsReadOnly(ScheduleFor(focus)[day - 1]);
        }

        /// <summary>The day a scenario is scheduled in this focus's week; 0 if unknown or a follow-up.</summary>
        public static int ScheduledDay(string scenarioId, string focus)
        {
            if (string.IsNullOrEmpty(scenarioId))
            {
                return 0;
            }

            string[][] schedule = ScheduleFor(focus);
            for (int d = 0; d < schedule.Length; d++)
            {
                if (Array.IndexOf(schedule[d], scenarioId) >= 0)
                {
                    return d + 1;
                }
            }

            return 0;
        }

        /// <summary>The day's first scenario: the shift opens once it is resolved. Null outside Days 1-7.</summary>
        public static string GateScenario(int day, string focus)
        {
            if (day < 1 || day > ChapterLength)
            {
                return null;
            }

            return ScheduleFor(focus)[day - 1][0];
        }

        public static bool IsFollowUp(string scenarioId) => Array.IndexOf(FollowUpIds, scenarioId) >= 0;

        // ------------------------------------------------------------------ follow-ups

        /// <summary>"family_callback@6".</summary>
        public static string FollowUpKey(string scenarioId, int day)
        {
            return (scenarioId ?? "") + "@" + day.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParseFollowUp(string key, out string scenarioId, out int day)
        {
            scenarioId = null;
            day = 0;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int at = key.LastIndexOf('@');
            if (at <= 0 || at == key.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(key.Substring(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
                || parsed <= 0)
            {
                return false;
            }

            scenarioId = key.Substring(0, at);
            day = parsed;
            return true;
        }

        // ------------------------------------------------------------------ active scenarios

        /// <summary>
        /// Active = (scheduled on a day &lt;= data.currentDay in this focus's schedule, or every scheduled scenario
        /// when useSchedule is false) or (a followUps key whose day &lt;= currentDay), and not completed.
        /// Order: today's gate first (so it heads its spot's queue), then by day, then position in the day;
        /// follow-ups after the scheduled scenarios of their day.
        /// </summary>
        public static List<string> ActiveScenarioIds(PlayerData data, bool useSchedule, string focus, string[] followUps)
        {
            var result = new List<string>();
            if (data == null)
            {
                return result;
            }

            int currentDay = data.currentDay;
            string[][] schedule = ScheduleFor(focus);

            // Follow-ups grouped by their day (first key per scenario wins; duplicates ignored).
            var followUpsByDay = new SortedDictionary<int, List<string>>();
            var seenFollowUps = new HashSet<string>();
            if (followUps != null)
            {
                foreach (string key in followUps)
                {
                    if (!TryParseFollowUp(key, out string id, out int day) || !IsFollowUp(id) || day > currentDay
                        || !seenFollowUps.Add(id))
                    {
                        continue;
                    }

                    if (!followUpsByDay.TryGetValue(day, out List<string> list))
                    {
                        list = new List<string>();
                        followUpsByDay[day] = list;
                    }

                    list.Add(id);
                }
            }

            int lastScheduledDay = useSchedule ? Math.Min(currentDay, ChapterLength) : ChapterLength;
            int lastDay = lastScheduledDay;
            foreach (int day in followUpsByDay.Keys)
            {
                lastDay = Math.Max(lastDay, day);
            }

            for (int d = 1; d <= lastDay; d++)
            {
                if (d <= lastScheduledDay && d <= ChapterLength)
                {
                    foreach (string id in schedule[d - 1])
                    {
                        AddIfOpen(data, result, id);
                    }
                }

                if (followUpsByDay.TryGetValue(d, out List<string> dayFollowUps))
                {
                    foreach (string id in dayFollowUps)
                    {
                        AddIfOpen(data, result, id);
                    }
                }
            }

            if (useSchedule)
            {
                string gate = GateScenario(currentDay, focus);
                int index = gate == null ? -1 : result.IndexOf(gate);
                if (index > 0)
                {
                    result.RemoveAt(index);
                    result.Insert(0, gate);
                }
            }

            return result;
        }

        static void AddIfOpen(PlayerData data, List<string> result, string id)
        {
            if (!result.Contains(id) && !IsCompleted(data, id))
            {
                result.Add(id);
            }
        }

        static bool IsCompleted(PlayerData data, string id)
        {
            return data.completedScenarioIds != null && Array.IndexOf(data.completedScenarioIds, id) >= 0;
        }

        /// <summary>The head of a spot's queue: the first active scenario at that spot, or null.</summary>
        public static string ActiveScenarioAtSpot(PlayerData data, string spotId, bool useSchedule, string focus,
                                                  string[] followUps)
        {
            foreach (string id in ActiveScenarioIds(data, useSchedule, focus, followUps))
            {
                if (SpotFor(id) == spotId)
                {
                    return id;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ copy

        /// <summary>The morning line for a day (tokens unfilled); null outside Days 1-7.</summary>
        public static string MorningLine(int day, string focus)
        {
            if (day < 1 || day > ChapterLength)
            {
                return null;
            }

            return MorningLines.TryGetValue(Key(ScheduleFor(focus)[day - 1]), out string line) ? line : null;
        }

        /// <summary>The reveal teaser for the day after endedDay; "Tomorrow is payday." after Day 7. Null for endedDay &lt; 1.</summary>
        public static string TeaserForNight(int endedDay, string focus)
        {
            if (endedDay >= ChapterLength)
            {
                return PaydayTeaserText;
            }

            if (endedDay < 1)
            {
                return null;
            }

            return Teasers.TryGetValue(Key(ScheduleFor(focus)[endedDay]), out string line) ? line : null;
        }

        /// <summary>"Tomorrow: your aunt calls back." for either call-back; null for anything else.</summary>
        public static string FollowUpTeaser(string scenarioId)
        {
            return IsFollowUp(scenarioId) ? FollowUpTeaserText : null;
        }

        // ------------------------------------------------------------------ spots and places

        /// <summary>The spot a scenario appears at (design spec 3.3); null if unknown.</summary>
        public static string SpotFor(string scenarioId)
        {
            switch (scenarioId)
            {
                case ScenarioLibrary.FoodDecisionId:
                case ScenarioLibrary.DataRunsOutId:
                case ScenarioLibrary.GroupChatContributionId:
                case ScenarioLibrary.KotaRunId:
                    return SpotCorner;
                case ScenarioLibrary.TransportDecisionId:
                case ScenarioLibrary.TaxiFareRiseId:
                    return SpotTaxi;
                case ScenarioLibrary.CreditBnplId:
                case ScenarioLibrary.StokvelDecisionId:
                    return SpotHub;
                case ScenarioLibrary.ImpulsePurchaseId:
                    return SpotShopfront;
                case ScenarioLibrary.FamilyObligationId:
                case ScenarioLibrary.FamilyCallbackId:
                case ScenarioLibrary.FamilyCallbackFullId:
                case ScenarioLibrary.EmergencyExpenseId:
                case ScenarioLibrary.DebitOrderCheckId:
                    return SpotGate;
                case ScenarioLibrary.MashonisaOfferId:
                case ScenarioLibrary.WindfallId:
                    return SpotEast;
                default:
                    return null;
            }
        }

        /// <summary>Generic place name for Mali's greeting and prompts, e.g. "the kota shop". "" for an unknown spot.</summary>
        public static string SpotPlaceName(string spotId, string focus, string travel)
        {
            Place(spotId, focus, travel, out string name, out _);
            return name;
        }

        /// <summary>
        /// Where a spot is, as Mali says it in her greeting (Revision 4): "at the kota shop", "at home", "down the road".
        /// "" for an unknown spot.
        /// </summary>
        public static string SpotWhere(string spotId, string focus, string travel)
        {
            string name = SpotPlaceName(spotId, focus, travel);
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            return spotId == SpotEast ? name : "at " + name;
        }

        /// <summary>Place label for the choice-sheet eyebrow and the CC summary, e.g. "Kota shop". "" for an unknown spot.</summary>
        public static string SpotPlaceLabel(string spotId, string focus, string travel)
        {
            Place(spotId, focus, travel, out _, out string label);
            return label;
        }

        static void Place(string spotId, string focus, string travel, out string name, out string label)
        {
            focus = NormalizeFocus(focus);
            travel = NormalizeTravel(travel);
            switch (spotId)
            {
                case SpotCorner:
                    if (focus == FocusFood)
                    {
                        name = "the kota shop";
                        label = "Kota shop";
                    }
                    else if (focus == FocusHomeFamily)
                    {
                        name = "the spaza shop";
                        label = "Spaza shop";
                    }
                    else
                    {
                        name = "the corner shop";
                        label = "Corner shop";
                    }
                    return;
                case SpotShopfront:
                    if (focus == FocusDataSocial)
                    {
                        name = "the clothing shop";
                        label = "Clothing shop";
                    }
                    else
                    {
                        name = "the shop window up the road";
                        label = "Shop window";
                    }
                    return;
                case SpotHub:
                    name = "the shops by the bank";
                    label = "Shops by the bank";
                    return;
                case SpotGate:
                    name = "home";
                    label = "Home";
                    return;
                case SpotEast:
                    name = "down the road";
                    label = "Down the road";
                    return;
                case SpotTaxi:
                    if (travel == TravelEHailing)
                    {
                        name = "the pick-up point";
                        label = "Pick-up point";
                    }
                    else if (travel == TravelCar)
                    {
                        name = "the petrol station";
                        label = "Petrol station";
                    }
                    else
                    {
                        name = "the taxi rank";
                        label = "Taxi rank";
                    }
                    return;
                default:
                    name = "";
                    label = "";
                    return;
            }
        }

        /// <summary>
        /// Labels of the first three distinct spots in this focus's schedule order (CC screen 3). The travel spot
        /// (its label follows the travel tap) is always one of the three (Revision 4): when it is not among the
        /// first three, it takes the third place.
        /// </summary>
        public static string[] WeekPlaces(string focus, string travel)
        {
            var spots = new List<string>();
            foreach (string[] day in ScheduleFor(focus))
            {
                foreach (string id in day)
                {
                    string spot = SpotFor(id);
                    if (spot != null && !spots.Contains(spot))
                    {
                        spots.Add(spot);
                    }

                    if (spots.Count == 3)
                    {
                        break;
                    }
                }

                if (spots.Count == 3)
                {
                    break;
                }
            }

            if (!spots.Contains(SpotTaxi))
            {
                if (spots.Count == 3)
                {
                    spots[2] = SpotTaxi;
                }
                else
                {
                    spots.Add(SpotTaxi);
                }
            }

            var labels = new string[spots.Count];
            for (int i = 0; i < spots.Count; i++)
            {
                labels[i] = SpotPlaceLabel(spots[i], focus, travel);
            }

            return labels;
        }
    }
}
