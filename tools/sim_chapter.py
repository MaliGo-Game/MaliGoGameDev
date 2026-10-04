"""
Chapter 1 economy simulation for MaliGo (docs/DESIGN_SPEC.md, Revision 3).

Plays the six reference play styles of spec section 3.2 through all 7 days for every
combination of the two spending-profile taps (4 x 4 = 16 profiles) and reports, per
combination: end totals, minimum cash, nights with something still owed, energy at shift
time, shifts worked, still owed + already promised going into payday. It then checks:

  * stuck paths   - a scenario where no choice is affordable (must never happen);
  * dominance     - a style that has the best end total AND is never worse than any other
                    style on energy spent on choices and on later effects (still owed +
                    promised for payday + follow-ups triggered). Must never happen;
  * fair choices  - inside every scenario (every travel mode, on its scheduled day for every
                    focus) no option is at least as good as every other option on all five
                    things its card shows: money over the week (Cash + Savings + Later), cash
                    kept today, savings kept today, energy, Later entries. Declining a want
                    (want=True) is exempt: its cost is going without the thing;
  * tightness     - the comfort path stays tight (it ends below the R1 000 start in every
                    profile, and comfort + loan has a night with something still owed in
                    every profile); no reference style ends more than R600 up.
  * random paths  - 3 000 random play-throughs per profile (random choices, random shift
                    decisions) never get stuck and never break the money invariant.
  * hidden stress - reported, not checked: the highest hidden stress over every path the script
                    plays (the six styles, a "stacked deferrals" path that keeps every shift, and
                    the random play-throughs), and the highest on paths that work all 7 shifts and
                    are never in arrears. Stacked deferrals reaching the stretched tone (60) is
                    intended (spec section 0); the stacked-deferrals path is not a reference style
                    and is not in the WP9 table.

The content below mirrors spec section 3.4 (choices), 3.1 (schedules per profile) and 3.4.0
(amounts per travel mode). If the spec changes, change this file in the same commit, rerun
it, and copy the printed reference table into WP9 (EconomySimTests).

Usage:
    python tools/sim_chapter.py              # full report; exit 0 = every check passes
    python tools/sim_chapter.py --brief      # only the reference table and the check results
Exit code: 0 = all checks pass, 1 = a check failed.
"""
import argparse
import itertools
import random
import sys

# ---------------------------------------------------------------- fixed numbers (spec section 0)
START_CASH, START_SAVINGS = 600, 400
DAILY_ENERGY, SHIFT_PAY, SHIFT_ENERGY = 100, 150, 60
CHAPTER_LENGTH, PAYDAY_DAY = 7, 8
ARREARS_STRESS, START_STRESS = 10, 25
BASE_OBLIGATIONS = [
    # id, label, short, amount, interval, nextDue, remaining, kind
    ("airtime", "Airtime", "Airtime", 60, 7, 2, -1, "bill"),
    ("rent", "Rent", "Rent", 500, 7, 3, -1, "bill"),
]

SPEND_FOCI = ["food", "transport", "data_social", "home_family"]
TRAVEL_MODES = ["taxi", "ehailing", "walk", "car"]

# ---------------------------------------------------------------- travel-mode amounts (spec 3.4.0)
# trip  = the "usual way" option of transport_decision: (cash out, energy used)
# daily = taxi_fare_rise "pay the new fare": (cash a day, energy used today); paid today, then
#         every night to Day 7. Bounded ranges (spec 3.4.0): trip R30-R50, daily R34-R50.
# walk_first = "Mostly on foot" lists the walk option first.
TRAVEL = {
    "taxi":     {"trip": (30, 5), "daily": (34, 5), "walk_first": False},
    "ehailing": {"trip": (44, 0), "daily": (50, 0), "walk_first": False},
    "walk":     {"trip": (30, 5), "daily": (34, 5), "walk_first": True},
    "car":      {"trip": (36, 0), "daily": (40, 0), "walk_first": False},
}
LIFT_CLUB = 120          # taxi_fare_rise lift club, the same for every mode


def C(cid, cash=0, sav=0, energy=0, stress=0, tag="Neutral", inst=None, follow=None, want=False):
    """A choice. energy is the energy it USES (positive number). inst = dict(count, amount,
    interval, first (absolute day, 0 = today + interval), last (absolute last day, 0 = none)).
    follow = (scenarioId, afterDays). want = declining something you'd enjoy (not a need)."""
    return {"id": cid, "cash": cash, "sav": sav, "energy": energy, "stress": stress, "tag": tag,
            "inst": inst, "follow": follow, "want": want}


def inst(count, amount, interval=2, first=0, last=0):
    return {"count": count, "amount": amount, "interval": interval, "first": first, "last": last}


def scenarios(travel):
    t = TRAVEL[travel]
    trip_cash, trip_energy = t["trip"]
    daily, daily_energy = t["daily"]
    trip_choices = [
        C("usual", cash=-trip_cash, energy=trip_energy, tag="Neutral"),
        C("walk", energy=45, tag="Frugal"),
        C("ride", cash=-90, stress=-2, tag="Discretionary"),
    ]
    if t["walk_first"]:
        trip_choices = [trip_choices[1], trip_choices[0], trip_choices[2]]
    return {
        "food_decision": {"spot": "CORNER", "need": True, "choices": [
            C("kota", cash=-50, stress=-5, tag="Discretionary"),
            C("vetkoek", cash=-20, stress=-2, tag="Frugal"),
            C("skip_lunch", energy=45, stress=5, tag="Deferred"),
        ]},
        "transport_decision": {"spot": "TAXI", "need": True, "choices": trip_choices},
        "data_runs_out": {"spot": "CORNER", "need": True, "choices": [
            C("bundle_1gb", cash=-85, tag="Neutral"),
            C("day_bundle", cash=-15, stress=2, tag="Deferred",
              inst=inst(2, 15, interval=1, last=CHAPTER_LENGTH)),
            C("free_wifi", energy=45, stress=3, tag="Frugal"),
        ]},
        "credit_bnpl": {"spot": "HUB", "need": False, "choices": [
            C("pay_in_full", cash=-360, tag="Discretionary"),
            C("pay_later", cash=-120, stress=3, tag="Deferred", inst=inst(2, 130, interval=2)),
            C("leave_it", tag="Frugal", want=True),
        ]},
        "impulse_purchase": {"spot": "SHOPFRONT", "need": False, "choices": [
            C("buy_it", cash=-120, stress=-2, tag="Discretionary"),
            C("walk_away", tag="Frugal", want=True),
            C("to_savings", cash=-120, sav=120, tag="Frugal", want=True),
        ]},
        "taxi_fare_rise": {"spot": "TAXI", "need": True, "choices": [
            C("pay_new_fare", cash=-daily, energy=daily_energy, tag="Neutral",
              inst=inst(4, daily, interval=1, last=CHAPTER_LENGTH)),
            C("lift_club", cash=-LIFT_CLUB, stress=-3, tag="Discretionary"),
            C("walk_today", energy=50, stress=2, tag="Frugal",
              inst=inst(4, daily, interval=1, last=CHAPTER_LENGTH)),
        ]},
        "family_obligation": {"spot": "GATE", "need": True, "choices": [
            C("send_full", cash=-200, tag="Neutral"),
            C("send_part", cash=-80, stress=3, tag="Neutral", follow=("family_callback", 2)),
            C("from_savings", sav=-200, tag="Neutral"),
            C("cant_this_week", stress=8, tag="Neutral", follow=("family_callback_full", 2)),
        ]},
        "family_callback": {"spot": "GATE", "need": True, "followup": True, "choices": [
            C("send_rest", cash=-120, tag="Neutral"),
            C("from_savings", sav=-120, tag="Neutral"),
            C("not_this_week", stress=6, tag="Neutral", want=True),
        ]},
        # set off only by cant_this_week: the aunt asks the full R200 (spec 3.4.7b)
        "family_callback_full": {"spot": "GATE", "need": True, "followup": True, "choices": [
            C("send_full", cash=-200, tag="Neutral"),
            C("from_savings", sav=-200, tag="Neutral"),
            C("not_this_week", stress=6, tag="Neutral", want=True),
        ]},
        "group_chat_contribution": {"spot": "CORNER", "need": False, "choices": [
            C("in_for_dinner", cash=-150, stress=-4, tag="Discretionary"),
            C("gift_only", cash=-50, stress=2, tag="Neutral"),
            C("not_this_time", stress=4, tag="Frugal", want=True),
        ]},
        "emergency_expense": {"spot": "GATE", "need": True, "choices": [
            C("from_savings", sav=-350, tag="Neutral"),
            C("from_cash", cash=-350, tag="Neutral"),
            C("cold_showers", energy=10, stress=12, tag="Deferred",
              inst=inst(1, 350, interval=2, first=PAYDAY_DAY)),
        ]},
        "mashonisa_offer": {"spot": "EAST", "need": False, "choices": [
            C("borrow_400", cash=400, stress=6, tag="Deferred", inst=inst(1, 600, interval=2)),
            C("borrow_200", cash=200, stress=3, tag="Deferred", inst=inst(1, 300, interval=2)),
            C("not_today", tag="Neutral", want=True),
        ]},
        "stokvel_decision": {"spot": "HUB", "need": False, "choices": [
            C("join", stress=-2, tag="Frugal", inst=inst(1, 200, first=PAYDAY_DAY)),
            C("not_for_now", tag="Neutral", want=True),
        ]},
        "windfall": {"spot": "EAST", "need": False, "choices": [
            C("all_to_savings", sav=300, tag="Frugal"),
            C("keep_cash", cash=300, stress=-5, tag="Discretionary"),
            C("half_half", cash=150, sav=150, stress=-2, tag="Neutral"),
        ]},
        "debit_order_check": {"spot": "GATE", "need": False, "choices": [
            C("move_to_cash", cash=199, sav=-199, stress=-3, tag="Neutral",
              inst=inst(1, 199, first=PAYDAY_DAY)),
            C("cancel_gym", tag="Frugal", want=True),
            C("leave_it", stress=6, tag="Deferred", inst=inst(1, 199, first=PAYDAY_DAY)),
        ]},
    }


# ---------------------------------------------------------------- schedules per spending focus (spec 3.1)
COMMON_TAIL = {
    2: ["data_runs_out", "credit_bnpl"],
    3: ["taxi_fare_rise", "impulse_purchase"],
    6: ["stokvel_decision", "windfall"],
    7: ["debit_order_check"],
}
SCHEDULES = {
    "food":        {1: ["food_decision", "transport_decision"], 4: ["family_obligation", "group_chat_contribution"],
                    5: ["emergency_expense", "mashonisa_offer"]},
    "transport":   {1: ["transport_decision", "food_decision"], 4: ["family_obligation", "group_chat_contribution"],
                    5: ["emergency_expense", "mashonisa_offer"]},
    "data_social": {1: ["food_decision", "group_chat_contribution"], 4: ["family_obligation", "transport_decision"],
                    5: ["emergency_expense", "mashonisa_offer"]},
    "home_family": {1: ["food_decision", "family_obligation"], 4: ["emergency_expense", "group_chat_contribution"],
                    5: ["transport_decision", "mashonisa_offer"]},
}
for _s in SCHEDULES.values():
    _s.update(COMMON_TAIL)


def schedule(focus):
    return [SCHEDULES[focus][d] for d in range(1, CHAPTER_LENGTH + 1)]


# ---------------------------------------------------------------- play styles (spec 3.2)
# Preference per scenario; if the preferred choice is unaffordable the style takes the next
# affordable one in its list, then in authored order.
SAVER = {
    "food_decision": ["skip_lunch"], "transport_decision": ["walk"], "data_runs_out": ["free_wifi"],
    "credit_bnpl": ["leave_it"], "impulse_purchase": ["to_savings", "walk_away"],
    "taxi_fare_rise": ["walk_today"], "family_obligation": ["cant_this_week"],
    "family_callback": ["not_this_week"], "family_callback_full": ["not_this_week"],
    "group_chat_contribution": ["not_this_time"],
    "emergency_expense": ["from_savings"], "mashonisa_offer": ["not_today"],
    "stokvel_decision": ["join"], "windfall": ["all_to_savings"], "debit_order_check": ["cancel_gym"],
}
MIDDLE = {
    "food_decision": ["vetkoek"], "transport_decision": ["usual"], "data_runs_out": ["day_bundle"],
    "credit_bnpl": ["pay_later"], "impulse_purchase": ["walk_away"], "taxi_fare_rise": ["pay_new_fare"],
    "family_obligation": ["send_part"], "family_callback": ["send_rest", "from_savings"],
    "group_chat_contribution": ["gift_only"], "emergency_expense": ["from_savings", "from_cash"],
    "mashonisa_offer": ["not_today"], "stokvel_decision": ["join"], "windfall": ["half_half"],
    "debit_order_check": ["leave_it"],
}
COMFORT = {
    "food_decision": ["kota"], "transport_decision": ["ride"], "data_runs_out": ["bundle_1gb"],
    "credit_bnpl": ["pay_in_full"], "impulse_purchase": ["buy_it"], "taxi_fare_rise": ["lift_club"],
    "family_obligation": ["send_full"], "family_callback": ["send_rest"],
    "group_chat_contribution": ["in_for_dinner"], "emergency_expense": ["from_cash"],
    "mashonisa_offer": ["not_today"], "stokvel_decision": ["join"], "windfall": ["keep_cash"],
    "debit_order_check": ["move_to_cash"],
}
COMFORT_LOAN = dict(COMFORT, mashonisa_offer=["borrow_400"])
# Not a reference style (not in the WP9 table): puts off everything it can while keeping every
# shift and every bill paid, to show how far stacked deferrals push hidden stress (spec section 0).
DEFERRER = {
    "food_decision": ["skip_lunch"], "transport_decision": ["walk"], "data_runs_out": ["day_bundle"],
    "credit_bnpl": ["pay_later"], "impulse_purchase": ["walk_away"],
    "taxi_fare_rise": ["walk_today", "pay_new_fare"], "family_obligation": ["cant_this_week"],
    "family_callback": ["not_this_week"], "family_callback_full": ["not_this_week"],
    "group_chat_contribution": ["not_this_time"], "emergency_expense": ["cold_showers"],
    "mashonisa_offer": ["borrow_400"], "stokvel_decision": ["not_for_now"], "windfall": ["all_to_savings"],
    "debit_order_check": ["leave_it"],
}

STYLES = [
    # name, preferences, works, keeps_shift (at the day's first scenario, avoid choices that leave < 60)
    ("saver", SAVER, True, False),
    ("middle", MIDDLE, True, False),
    ("comfort", COMFORT, True, False),
    ("comfort_loan", COMFORT_LOAN, True, False),
    ("never_works", MIDDLE, False, False),
    ("always_works", SAVER, True, True),
]


# ---------------------------------------------------------------- engine (mirrors spec 2.4, 7.3, 7.6)
class State:
    def __init__(self, focus, travel):
        self.focus, self.travel = focus, travel
        self.content = scenarios(travel)
        self.sched = schedule(focus)
        self.day = 1
        self.cash, self.sav = START_CASH, START_SAVINGS
        self.energy = DAILY_ENERGY
        self.stress = START_STRESS
        self.obl = [dict(id=o[0], label=o[1], short=o[2], amount=o[3], interval=o[4], next=o[5],
                         remaining=o[6], kind=o[7], arrears=0.0, created=0) for o in BASE_OBLIGATIONS]
        self.completed = []
        self.followups = []          # (scenarioId, day)
        self.choices = []            # (day, scenarioId, choiceId)
        self.worked_day = 0
        self.shifts = 0
        self.min_cash = self.cash
        self.max_stress = self.stress
        self.arrears_nights = []
        self.energy_at_shift = {}
        self.energy_used = 0
        self.followups_triggered = 0
        self.days_total = []         # end-of-night totals
        self.stuck = []
        self.events = []             # (day, cashDelta, savDelta) for the invariant
        self.trace = False

    # schedule helpers (ChapterSchedule)
    def gate(self):
        return self.sched[self.day - 1][0]

    def active(self):
        out = []
        for d in range(1, self.day + 1):
            for sid in self.sched[d - 1]:
                if sid not in self.completed:
                    out.append(sid)
        for sid, d in self.followups:
            if d <= self.day and sid not in self.completed:
                out.append(sid)
        return out

    def affordable(self, c):
        return self.cash + c["cash"] >= -0.005 and self.sav + c["sav"] >= -0.005

    def apply_money(self, dc, ds, label=""):
        assert self.cash + dc >= -0.005 and self.sav + ds >= -0.005, "recorder refused a negative pool"
        self.cash += dc
        self.sav += ds
        self.events.append((self.day, dc, ds))
        if self.trace:
            print("    Day %d  %-34s cash %+5d sav %+5d  -> cash %5d sav %5d" % (
                self.day, label, dc, ds, self.cash, self.sav))
        self.min_cash = min(self.min_cash, self.cash)

    def resolve(self, sid, cid):
        s = self.content[sid]
        c = next(x for x in s["choices"] if x["id"] == cid)
        assert self.affordable(c)
        if c["cash"] or c["sav"]:
            self.apply_money(c["cash"], c["sav"], sid + "/" + cid)
        elif self.trace:
            print("    Day %d  %-34s (no money; energy %d)" % (self.day, sid + "/" + cid, c["energy"]))
        self.energy_used += c["energy"]
        self.energy = max(0, min(DAILY_ENERGY, self.energy - c["energy"]))
        self.stress = max(0, min(100, self.stress + c["stress"]))
        self.max_stress = max(self.max_stress, self.stress)
        self.completed.append(sid)
        self.choices.append((self.day, sid, cid))
        i = c["inst"]
        if i:
            first = i["first"] if i["first"] > 0 else self.day + i["interval"]
            count = i["count"]
            if i["last"] > 0:
                count = 0 if first > i["last"] else min(count, (i["last"] - first) // i["interval"] + 1)
            if count > 0:
                self.obl.append(dict(id=sid + "_" + cid, label=sid, short=sid, amount=i["amount"],
                                     interval=i["interval"], next=first, remaining=count, kind="instalment",
                                     arrears=0.0, created=self.day))
        f = c["follow"]
        if f and self.day + f[1] <= CHAPTER_LENGTH:
            self.followups.append((f[0], self.day + f[1]))
            self.followups_triggered += 1
        return c

    def shift_state(self):
        if self.worked_day == self.day:
            return "done"
        if self.gate() not in self.completed:
            return "not_yet"
        if self.energy < SHIFT_ENERGY:
            return "tired"
        return "open"

    def do_shift(self):
        assert self.shift_state() == "open"
        self.energy_at_shift[self.day] = self.energy
        self.apply_money(SHIFT_PAY, 0, "shift (energy before %d)" % self.energy)
        self.energy -= SHIFT_ENERGY
        self.worked_day = self.day
        self.shifts += 1

    def settle(self):
        owed_total = 0.0
        keep = []
        for o in self.obl:
            newly = 0
            while o["remaining"] != 0 and o["next"] <= self.day:
                newly += o["amount"]
                o["next"] += max(1, o["interval"])
                if o["remaining"] > 0:
                    o["remaining"] -= 1
            owed = o["arrears"] + newly
            if owed > 0.005:
                paid = min(max(0, self.cash), owed)
                if paid > 0.005:
                    self.apply_money(-paid, 0, "night: " + o["id"])
                o["arrears"] = owed - paid if owed - paid > 0.005 else 0.0
                owed_total += o["arrears"]
            if not (o["remaining"] == 0 and o["arrears"] <= 0.005):
                keep.append(o)
        self.obl = keep
        if owed_total > 0.005:
            self.stress = min(100, self.stress + ARREARS_STRESS)
            self.max_stress = max(self.max_stress, self.stress)
            self.arrears_nights.append(self.day)
        self.days_total.append(self.cash + self.sav)
        if self.trace:
            print("    Night %d closes: cash %d, savings %d, total %d, still owed %d" % (
                self.day, self.cash, self.sav, self.cash + self.sav, sum(o["arrears"] for o in self.obl)))

    def end_day(self):
        self.settle()
        if self.day < CHAPTER_LENGTH:
            self.day += 1
            self.energy = DAILY_ENERGY

    def still_owed(self):
        return sum(o["arrears"] for o in self.obl)

    def promised(self):
        tot = 0
        for o in self.obl:
            if o["remaining"] > 0:
                n, d = 0, o["next"]
                for _ in range(o["remaining"]):
                    if d >= PAYDAY_DAY:
                        n += 1
                    d += max(1, o["interval"])
                tot += n * o["amount"]
        return tot


def money_cost(state, c):
    """Money a choice costs over the chapter (out now + every later charge), as the
    choice card shows it in Cash, Savings and Later."""
    cost = -(c["cash"] + c["sav"])
    i = c["inst"]
    if i:
        first = i["first"] if i["first"] > 0 else state.day + i["interval"]
        count = i["count"]
        if i["last"] > 0:
            count = 0 if first > i["last"] else min(count, (i["last"] - first) // i["interval"] + 1)
        cost += max(0, count) * i["amount"]
    return cost


def pick(state, sid, prefs, keep_shift):
    s = state.content[sid]
    avail = [c for c in s["choices"] if state.affordable(c)]
    if not avail:
        state.stuck.append((state.day, sid))
        return None
    if keep_shift and sid == state.gate() and state.worked_day != state.day:
        ok = [c for c in avail if state.energy - c["energy"] >= SHIFT_ENERGY]
        pref = [c for c in ok if c["id"] in prefs]
        if pref:
            return pref[0]["id"]
        if ok:
            best = min(ok, key=lambda c: money_cost(state, c))    # ties -> authored order
            return best["id"]
    for cid in prefs:
        for c in avail:
            if c["id"] == cid:
                return cid
    return avail[0]["id"]


def play(focus, travel, prefs, works, keep_shift, trace=False):
    st = State(focus, travel)
    st.trace = trace
    for _ in range(CHAPTER_LENGTH):
        gate = st.gate()
        if gate in st.active():
            cid = pick(st, gate, prefs.get(gate, []), keep_shift)
            if cid:
                st.resolve(gate, cid)
        if works and st.shift_state() == "open":
            st.do_shift()
        elif works and st.worked_day != st.day:
            st.energy_at_shift.setdefault(st.day, st.energy)   # wanted to work: record why not
        for sid in st.active():
            cid = pick(st, sid, prefs.get(sid, []), keep_shift)
            if cid:
                st.resolve(sid, cid)
        st.end_day()
    return st


def invariant_ok(st):
    cash, sav = START_CASH, START_SAVINGS
    for _, dc, ds in st.events:
        cash += dc
        sav += ds
        if cash < -0.005 or sav < -0.005:
            return False
    return abs(cash - st.cash) < 0.01 and abs(sav - st.sav) < 0.01


def play_random(focus, travel, rng):
    st = State(focus, travel)
    for _ in range(CHAPTER_LENGTH):
        order = st.active()
        rng.shuffle(order)
        work_slot = rng.randint(0, len(order) + 1)
        for k in range(len(order) + 1):
            if k == work_slot and st.shift_state() == "open":
                st.do_shift()
            if k < len(order):
                sid = order[k]
                if sid in st.completed or sid not in st.active():
                    continue
                if rng.random() < 0.1:
                    continue                      # "Not now": carries over
                avail = [c for c in st.content[sid]["choices"] if st.affordable(c)]
                if not avail:
                    st.stuck.append((st.day, sid))
                    continue
                st.resolve(sid, rng.choice(avail)["id"])
        st.end_day()
    return st


# ---------------------------------------------------------------- checks
def fair_choice_check(travel, day_of):
    """Inside each scenario, no option may be at least as good as every other option on all
    five things its card shows: money over the chapter (Cash + Savings + Later), cash kept
    today, savings kept today, energy, and Later entries (payments or a follow-up). The one
    exception is declining a want (want=True), whose cost is going without the thing itself."""
    problems = []
    content = scenarios(travel)
    for sid, s in content.items():
        st = State("food", travel)
        st.day = day_of.get(sid, 1)
        rows = []
        for c in s["choices"]:
            later_money = money_cost(st, c) + (c["cash"] + c["sav"])
            later = (1 if later_money > 0 else 0) + (1 if c["follow"] else 0)
            rows.append((c, money_cost(st, c), c["cash"], c["sav"], c["energy"], later))
        for c, m, cash_now, sav_now, e, later in rows:
            if c["want"]:
                continue
            others = [r for r in rows if r[0] is not c]
            if others and all(m <= o[1] and cash_now >= o[2] and sav_now >= o[3] and e <= o[4] and later <= o[5]
                              for o in others):
                problems.append("%s/%s (Day %d) is at least as good as every other option"
                                % (sid, c["id"], st.day))
    return problems


def first_day(focus):
    out = {}
    for d, ids in enumerate(schedule(focus), start=1):
        for sid in ids:
            out[sid] = d
    out["family_callback"] = out["family_obligation"] + 2
    out["family_callback_full"] = out["family_obligation"] + 2
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--brief", action="store_true")
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--trace", nargs=3, metavar=("STYLE", "FOCUS", "TRAVEL"),
                    help="print every money event of one play-through, e.g. --trace middle food taxi")
    args = ap.parse_args()
    if args.trace:
        name, focus, travel = args.trace
        style = next(s for s in STYLES if s[0] == name)
        st = play(focus, travel, style[1], style[2], style[3], trace=True)
        print("End: cash %d, savings %d, total %d, still owed %d, promised for payday %d" % (
            st.cash, st.sav, st.cash + st.sav, st.still_owed(), st.promised()))
        return 0

    failures = []
    results = {}
    for focus, travel in itertools.product(SPEND_FOCI, TRAVEL_MODES):
        for name, prefs, works, keep in STYLES:
            st = play(focus, travel, prefs, works, keep)
            results[(focus, travel, name)] = st
            if st.stuck:
                failures.append("stuck: %s/%s %s %s" % (focus, travel, name, st.stuck))
            if not invariant_ok(st):
                failures.append("invariant: %s/%s %s" % (focus, travel, name))

    # ---- report
    names = [s[0] for s in STYLES]
    print("MaliGo Chapter 1 simulation (spec Revision 3)")
    print("End total = cash + savings on the night of Day 7 (start R1 000).")
    print()
    print("Reference end totals (WP9 EconomySimTests):")
    print("%-24s" % "profile" + "".join("%14s" % n for n in names))
    for focus, travel in itertools.product(SPEND_FOCI, TRAVEL_MODES):
        print("%-24s" % (focus + "/" + travel) +
              "".join("%14d" % round(results[(focus, travel, n)].cash + results[(focus, travel, n)].sav) for n in names))
    print()
    if not args.brief:
        for focus, travel in itertools.product(SPEND_FOCI, TRAVEL_MODES):
            print("== %s / %s" % (focus, travel))
            print("   %-13s %6s %6s %6s %7s %-13s %6s %6s %6s %6s %6s %6s" % (
                "style", "cash", "sav", "total", "minCash", "arrearsNights", "shifts",
                "eUsed", "owed", "prom", "follow", "stress"))
            for n in names:
                st = results[(focus, travel, n)]
                es = ",".join(str(st.energy_at_shift.get(d, "-")) for d in range(1, 8))
                print("   %-13s %6d %6d %6d %7d %-13s %6d %6d %6d %6d %6d %6d" % (
                    n, st.cash, st.sav, st.cash + st.sav, st.min_cash,
                    ",".join(map(str, st.arrears_nights)) or "-", st.shifts, st.energy_used,
                    st.still_owed(), st.promised(), st.followups_triggered, st.max_stress))
                print("   %-13s energy before each day's shift (Days 1-7): %s" % ("", es))
            print()

    # ---- dominance between styles, per profile
    # A style dominates when it has the best end total AND, against every other style, it
    # is never worse on: energy used on choices, still owed at payday, promised for payday,
    # follow-ups it set off. (Wants it went without are listed, not scored.)
    def later_vec(st):
        return (st.energy_used, st.still_owed(), st.promised(), st.followups_triggered)

    dom_lines = []
    for focus, travel in itertools.product(SPEND_FOCI, TRAVEL_MODES):
        sts = {n: results[(focus, travel, n)] for n in names}
        totals = {n: s.cash + s.sav for n, s in sts.items()}
        best = max(totals.values())
        for n, s in sts.items():
            if totals[n] < best:
                continue
            mine = later_vec(s)
            beaten_by = []
            for m, o in sts.items():
                if m == n:
                    continue
                theirs = later_vec(o)
                axes = [ax for ax, a_, b_ in zip(("energy", "owed", "promised", "follow-ups"), mine, theirs) if a_ > b_]
                if axes:
                    beaten_by.append("%s (%s)" % (m, "/".join(axes)))
            if not beaten_by:
                failures.append("dominant: %s/%s %s" % (focus, travel, n))
            else:
                wants = sum(1 for (_, sid, cid) in s.choices
                            if next(c for c in s.content[sid]["choices"] if c["id"] == cid)["want"])
                dom_lines.append("%-22s best money: %s R%d, went without %d wants; worse than %s" % (
                    focus + "/" + travel, n, totals[n], wants, ", ".join(beaten_by)))
    print("Dominance (best end total AND never worse on energy used, still owed, promised, follow-ups):")
    for line in dom_lines:
        print("  " + line)
    print()

    # ---- hidden stress: only paths with arrears or a lost shift reach Mali's stretched tone
    for (focus, travel, n), st in results.items():
        lost_shift = STYLES[names.index(n)][2] and st.shifts < CHAPTER_LENGTH
        if st.max_stress >= 60 and not st.arrears_nights and not lost_shift:
            failures.append("stress >= 60 without arrears or a lost shift: %s/%s %s" % (focus, travel, n))

    # ---- hidden stress over every path played (reported; stacked deferrals reaching 60 is intended)
    stress_all = max(st.max_stress for st in results.values())
    stress_clean = 0          # paths that work all 7 shifts and are never in arrears
    for st in results.values():
        if st.shifts == CHAPTER_LENGTH and not st.arrears_nights:
            stress_clean = max(stress_clean, st.max_stress)
    defer_lines = []
    for focus, travel in itertools.product(SPEND_FOCI, TRAVEL_MODES):
        st = play(focus, travel, DEFERRER, True, True)
        if st.stuck:
            failures.append("stuck: %s/%s deferrer %s" % (focus, travel, st.stuck))
        if not invariant_ok(st):
            failures.append("invariant: %s/%s deferrer" % (focus, travel))
        stress_all = max(stress_all, st.max_stress)
        if st.shifts == CHAPTER_LENGTH and not st.arrears_nights:
            stress_clean = max(stress_clean, st.max_stress)
        defer_lines.append("%-22s max stress %3d, shifts %d, nights still owing %s" % (
            focus + "/" + travel, st.max_stress, st.shifts, ",".join(map(str, st.arrears_nights)) or "-"))
    if not args.brief:
        print("Stacked deferrals (keeps every shift; not a reference style):")
        for line in defer_lines:
            print("  " + line)
        print()

    # ---- fair choices inside scenarios
    for travel in TRAVEL_MODES:
        for focus in SPEND_FOCI:
            for p in fair_choice_check(travel, first_day(focus)):
                failures.append("fair-choice (%s/%s): %s" % (focus, travel, p))

    # ---- tightness
    for focus, travel in itertools.product(SPEND_FOCI, TRAVEL_MODES):
        c = results[(focus, travel, "comfort")]
        cl = results[(focus, travel, "comfort_loan")]
        if c.cash + c.sav >= START_CASH + START_SAVINGS:
            failures.append("comfort not tight: %s/%s ends R%d" % (focus, travel, c.cash + c.sav))
        if not cl.arrears_nights:
            failures.append("comfort+loan never short: %s/%s" % (focus, travel))
        for n in names:
            s = results[(focus, travel, n)]
            if s.cash + s.sav > START_CASH + START_SAVINGS + 600:
                failures.append("too easy: %s/%s %s ends R%d" % (focus, travel, n, s.cash + s.sav))

    # ---- random play-throughs
    rng = random.Random(args.seed)
    rnd_min, rnd_max = 10 ** 9, -10 ** 9
    for focus, travel in itertools.product(SPEND_FOCI, TRAVEL_MODES):
        for _ in range(3000):
            st = play_random(focus, travel, rng)
            if st.stuck:
                failures.append("random stuck: %s/%s %s" % (focus, travel, st.stuck[:1]))
                break
            if not invariant_ok(st):
                failures.append("random invariant: %s/%s" % (focus, travel))
                break
            rnd_min = min(rnd_min, st.cash + st.sav)
            rnd_max = max(rnd_max, st.cash + st.sav)
            stress_all = max(stress_all, st.max_stress)
            if st.shifts == CHAPTER_LENGTH and not st.arrears_nights:
                stress_clean = max(stress_clean, st.max_stress)
    print("Random play-throughs (3 000 per profile): end totals from R%d to R%d, none stuck." % (rnd_min, rnd_max))
    print("Highest hidden stress over every path played: %d; on paths with all 7 shifts and no night owing: %d"
          " (stretched tone at 60)." % (stress_all, stress_clean))
    print()

    if failures:
        print("CHECKS FAILED:")
        for f in failures:
            print("  - " + f)
        return 1
    print("All checks pass: no stuck path, no dominant style, no option better on everything its card shows,")
    print("comfort ends below R1 000 in every profile and comfort + loan is short at least one night.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
