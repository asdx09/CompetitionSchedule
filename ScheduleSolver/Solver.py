from ortools.sat.python import cp_model
from models import ScheduleRequestForSolver


# ============================================================
# Helpers
# ============================================================

def add_forbidden_interval(
    model,
    start_var,
    end_var,
    forbidden_start,
    forbidden_end,
    entry_id,
    enforce_if=None
):
    before = model.NewBoolVar(f"entry_{entry_id}_before_forbidden")
    after = model.NewBoolVar(f"entry_{entry_id}_after_forbidden")

    c1 = model.Add(end_var <= forbidden_start)
    c2 = model.Add(end_var > forbidden_start)
    c3 = model.Add(start_var >= forbidden_end)
    c4 = model.Add(start_var < forbidden_end)
    c5 = model.AddBoolOr([before, after])

    c1.OnlyEnforceIf(before)
    c2.OnlyEnforceIf(before.Not())
    c3.OnlyEnforceIf(after)
    c4.OnlyEnforceIf(after.Not())

    if enforce_if is not None:
        c1.OnlyEnforceIf(enforce_if)
        c2.OnlyEnforceIf(enforce_if)
        c3.OnlyEnforceIf(enforce_if)
        c4.OnlyEnforceIf(enforce_if)
        c5.OnlyEnforceIf(enforce_if)


def add_span(model, starts, ends, name, horizon):
    min_start = model.NewIntVar(0, horizon, f"{name}_min_start")
    max_end = model.NewIntVar(0, horizon, f"{name}_max_end")
    span = model.NewIntVar(0, horizon, f"{name}_span")

    model.AddMinEquality(min_start, starts)
    model.AddMaxEquality(max_end, ends)
    model.Add(span == max_end - min_start)

    return span


class TravelLookup:
    """
    Pre-flattened location x location -> travel time matrix, looked up with
    a single AddElement call per (entry, entry) pair instead of rebuilding
    an AND/OR boolvar chain over every travel-table row for every pair.

    Works on *compact* location indices (0..n-1). Callers that only have the
    real event_location variable (domain = actual location ids) should use
    `idx_var_for(event_id)`, which channels the real-id variable to its
    compact-index twin exactly once per event (not once per pair).
    """

    def __init__(self, model, req: ScheduleRequestForSolver):
        self._model = model
        self.loc_ids = sorted(l.id for l in req.locations)          # index -> real id
        self.idx = {lid: i for i, lid in enumerate(self.loc_ids)}   # real id -> index
        self.n = len(self.loc_ids)

        matrix = [[req.basePauseTime] * self.n for _ in range(self.n)]
        for t in req.travel:
            i = self.idx.get(t.locationID1)
            j = self.idx.get(t.locationID2)
            if i is not None and j is not None:
                matrix[i][j] = t.pause

        self.flat_pause = [matrix[i][j] for i in range(self.n) for j in range(self.n)]
        self._lo = min(self.flat_pause)
        self._hi = max(self.flat_pause)
        self._idx_var_cache = {}
        self._pair_cache = {}

    def idx_var_for(self, event_id, real_loc_var, allowed_real_ids):
        """Compact-index twin of a real-location-id IntVar, built once per event."""
        cached = self._idx_var_cache.get(event_id)
        if cached is not None:
            return cached

        allowed_idx = sorted(self.idx[l] for l in allowed_real_ids if l in self.idx)
        idx_var = self._model.NewIntVarFromDomain(
            cp_model.Domain.FromValues(allowed_idx), f"event_{event_id}_loc_idx"
        )
        # channel: loc_ids[idx_var] == real_loc_var  (one AddElement per event)
        self._model.AddElement(idx_var, self.loc_ids, real_loc_var)

        self._idx_var_cache[event_id] = idx_var
        return idx_var

    def time_between(self, event_id1, idx_var1, event_id2, idx_var2):
        """Travel time between two events' locations, cached per unordered
        event-id pair so repeated calls (e.g. from different constraint
        sections) reuse the same AddElement lookup."""
        key = (event_id1, event_id2) if event_id1 <= event_id2 else (event_id2, event_id1)
        cached = self._pair_cache.get(key)
        if cached is not None:
            return cached

        n = self.n
        model = self._model
        flat_idx = model.NewIntVar(0, n * n - 1, f"travel_flatidx_{key[0]}_{key[1]}")
        model.Add(flat_idx == idx_var1 * n + idx_var2)
        t = model.NewIntVar(self._lo, self._hi, f"traveltime_{key[0]}_{key[1]}")
        model.AddElement(flat_idx, self.flat_pause, t)

        self._pair_cache[key] = t
        return t


# ============================================================
# Main solve
# ============================================================

def schedule(req: ScheduleRequestForSolver, printer, container):
    print("Solver started!")
    horizon = req.dayLength * req.maxDays
    model = cp_model.CpModel()

    loc_by_id = {l.id: l for l in req.locations}
    comp_by_id = {c.id: c for c in req.competitors}
    event_by_id = {e.id: e for e in req.events}

    travel = TravelLookup(model, req)

    start, end, interval = {}, {}, {}
    event_location = {}       # event_id -> IntVar, domain = real location ids (unchanged externally)
    event_location_idx = {}   # event_id -> IntVar, compact index twin (for fast travel lookups)

    # ---------------- Event location ----------------
    for ev in req.events:
        event_location[ev.id] = model.NewIntVarFromDomain(
            cp_model.Domain.FromValues(ev.possibleLocations),
            f"event_loc_{ev.id}"
        )
        event_location_idx[ev.id] = travel.idx_var_for(
            ev.id, event_location[ev.id], ev.possibleLocations
        )

    # ---------------- Entry intervals ----------------
    for e in req.entries:
        ev = event_by_id[e.eventId]
        possible_starts = list(range(0, horizon - ev.duration, 5))
        start[e.id] = model.NewIntVarFromDomain(cp_model.Domain.FromValues(possible_starts), f"start_{e.id}")
        end[e.id] = model.NewIntVar(ev.duration, horizon, f"end_{e.id}")
        interval[e.id] = model.NewIntervalVar(start[e.id], ev.duration, end[e.id], f"interval_{e.id}")

    printer.start_vars.update(start)
    printer.end_vars.update(end)
    printer.event_location_vars.update(event_location)

    # ---------------- Per-competitor no-overlap + travel ordering ----------------
    for comp in req.competitors:
        comp_entries = [e for e in req.entries if e.competitorId == comp.id]
        intervals = [interval[e.id] for e in comp_entries]

        if len(intervals) > 1:
            model.AddNoOverlap(intervals)

        for i in range(len(comp_entries)):
            for j in range(i + 1, len(comp_entries)):
                e1 = comp_entries[i]
                e2 = comp_entries[j]

                travel_time = travel.time_between(
                    e1.eventId, event_location_idx[e1.eventId],
                    e2.eventId, event_location_idx[e2.eventId]
                )

                order = model.NewBoolVar(f"order_{e1.id}_{e2.id}")
                model.Add(start[e2.id] >= end[e1.id] + travel_time).OnlyEnforceIf(order)
                model.Add(start[e1.id] >= end[e2.id] + travel_time).OnlyEnforceIf(order.Not())

    # ---------------- Location capacity + break_time ----------------
    for loc in req.locations:
        loc_intervals = []
        for e in req.entries:
            ev = event_by_id[e.eventId]
            if loc.id in ev.possibleLocations:
                is_here = model.NewBoolVar(f"entry_{e.id}_on_loc_{loc.id}")
                opt_interval = model.NewOptionalIntervalVar(
                    start[e.id],
                    ev.duration + req.breakTimeLoc,
                    end[e.id] + req.breakTimeLoc,
                    is_here,
                    f"optint_{e.id}_loc{loc.id}"
                )
                model.Add(event_location[e.eventId] == loc.id).OnlyEnforceIf(is_here)
                model.Add(event_location[e.eventId] != loc.id).OnlyEnforceIf(is_here.Not())
                loc_intervals.append(opt_interval)
        if loc_intervals:
            model.AddCumulative(loc_intervals, [1] * len(loc_intervals), loc.capacity)

    # ---------------- Same-location wave/ordering (de-duplicated) ----------------
    # The original version looped per-location and re-derived "is this pair
    # at location X" for every location the pair could jointly use, which
    # silently duplicated constraints whenever two events shared more than
    # one possible location. Looping over entry pairs once and comparing
    # their event_location variables directly removes that duplication and
    # collapses an O(locations x entries^2) block down to O(entries^2).
    for i in range(len(req.entries)):
        e1 = req.entries[i]
        ev1 = event_by_id[e1.eventId]
        for j in range(i + 1, len(req.entries)):
            e2 = req.entries[j]
            ev2 = event_by_id[e2.eventId]

            # Can never share a location -> nothing to constrain.
            if not (set(ev1.possibleLocations) & set(ev2.possibleLocations)):
                continue

            if e1.eventId == e2.eventId:
                # Same event => same event_location variable by construction,
                # "both at this location" is trivially true, no boolvar needed
                # to prove it.
                same_start = model.NewBoolVar(f"same_start_{e1.id}_{e2.id}")
                model.Add(start[e1.id] == start[e2.id]).OnlyEnforceIf(same_start)
                model.Add(start[e1.id] != start[e2.id]).OnlyEnforceIf(same_start.Not())

                e1_before_e2 = model.NewBoolVar(f"e{e1.id}_before_e{e2.id}")
                model.Add(end[e1.id] <= start[e2.id]).OnlyEnforceIf([e1_before_e2, same_start.Not()])
                model.Add(end[e2.id] <= start[e1.id]).OnlyEnforceIf([e1_before_e2.Not(), same_start.Not()])
            else:
                same_loc = model.NewBoolVar(f"same_loc_{e1.id}_{e2.id}")
                model.Add(event_location[e1.eventId] == event_location[e2.eventId]).OnlyEnforceIf(same_loc)
                model.Add(event_location[e1.eventId] != event_location[e2.eventId]).OnlyEnforceIf(same_loc.Not())

                # Different event types sharing a location can't start together
                # (this mirrors the original rule: same_start forced to 0
                # whenever e1.eventId != e2.eventId).
                e1_before_e2 = model.NewBoolVar(f"e{e1.id}_before_e{e2.id}")
                model.Add(end[e1.id] <= start[e2.id]).OnlyEnforceIf([e1_before_e2, same_loc])
                model.Add(end[e2.id] <= start[e1.id]).OnlyEnforceIf([e1_before_e2.Not(), same_loc])

    # ---------------- Group-level single-location-at-a-time constraint ----------------
    groups = {}
    for comp in req.competitors:
        if comp.groupId != -1:
            groups.setdefault(comp.groupId, []).append(comp.id)

    for groupId, member_ids in groups.items():
        group_entries = [e for e in req.entries if comp_by_id[e.competitorId].groupId == groupId]

        for i in range(len(group_entries)):
            for j in range(i + 1, len(group_entries)):
                e1 = group_entries[i]
                e2 = group_entries[j]

                same_loc = model.NewBoolVar(f"group{groupId}_entry{e1.id}_{e2.id}_same_loc")
                model.Add(event_location[e1.eventId] == event_location[e2.eventId]).OnlyEnforceIf(same_loc)
                model.Add(event_location[e1.eventId] != event_location[e2.eventId]).OnlyEnforceIf(same_loc.Not())

                travel_time = travel.time_between(
                    e1.eventId, event_location_idx[e1.eventId],
                    e2.eventId, event_location_idx[e2.eventId]
                )

                e1_before_e2 = model.NewBoolVar(f"group_e1_before_e2_{e1.id}_{e2.id}")
                model.Add(start[e2.id] >= end[e1.id] + travel_time).OnlyEnforceIf([e1_before_e2, same_loc.Not()])
                model.Add(start[e1.id] >= end[e2.id] + travel_time).OnlyEnforceIf([e1_before_e2.Not(), same_loc.Not()])

    # ---------------- Constraints ----------------
    for cons in req.constraints:
        affected_entries = []
        if cons.constraintType == 'C':
            affected_entries = [e for e in req.entries if e.competitorId == cons.objectId]

        elif cons.constraintType == 'L':
            for e in req.entries:
                ev = event_by_id[e.eventId]
                # If this event can never be placed at cons.objectId, the
                # constraint can never bind for this entry — skip it, no
                # variable needed.
                if cons.objectId not in ev.possibleLocations:
                    continue

                is_on_loc = model.NewBoolVar(f"entry_{e.id}_on_loc_{cons.objectId}")
                model.Add(event_location[e.eventId] == cons.objectId).OnlyEnforceIf(is_on_loc)
                model.Add(event_location[e.eventId] != cons.objectId).OnlyEnforceIf(is_on_loc.Not())

                add_forbidden_interval(
                    model, start[e.id], end[e.id],
                    cons.startTime, cons.endTime, e.id,
                    enforce_if=is_on_loc
                )

        elif cons.constraintType == 'T':
            # event_by_id[e.eventId].id is always equal to e.eventId by
            # definition (that's what the dict key is), so the original
            # per-entry boolvar that "checked" this was verifying a Python
            # constant at solve time. A plain filter is equivalent and free.
            affected_entries = [e for e in req.entries if e.eventId == cons.objectId]

        elif cons.constraintType == 'G':
            group_members = [c.id for c in req.competitors if c.groupId == cons.objectId]
            affected_entries = [e for e in req.entries if e.competitorId in group_members]

        for e in affected_entries:
            add_forbidden_interval(model, start[e.id], end[e.id], cons.startTime, cons.endTime, e.id)

    # ---------------- Group-level span ----------------
    group_event_entries = {}
    for comp in req.competitors:
        if comp.groupId != -1:
            groupId = comp.groupId
            group_event_entries.setdefault(groupId, {})
            for e in req.entries:
                if e.competitorId == comp.id:
                    group_event_entries[groupId].setdefault(e.eventId, []).append(e.id)

    group_event_spans = []
    for groupId, events in group_event_entries.items():
        for eventId, entry_ids in events.items():
            if len(entry_ids) > 1:
                starts = [start[eid] for eid in entry_ids]
                ends = [end[eid] for eid in entry_ids]
                span = add_span(model, starts, ends, f"group{groupId}_event{eventId}", horizon)
                group_event_spans.append(span)

    event_spans = []
    for ev in req.events:
        ev_entries = [e for e in req.entries if e.eventId == ev.id]
        if len(ev_entries) > 1:
            ev_starts = [start[e.id] for e in ev_entries]
            ev_ends = [end[e.id] for e in ev_entries]
            span = add_span(model, ev_starts, ev_ends, f"event_{ev.id}", horizon)
            event_spans.append(span)

    competitor_spans = []
    for comp in req.competitors:
        comp_entries = [e for e in req.entries if e.competitorId == comp.id]
        if len(comp_entries) > 1:
            comp_starts = [start[e.id] for e in comp_entries]
            comp_ends = [end[e.id] for e in comp_entries]
            span = add_span(model, comp_starts, comp_ends, f"competitor_{comp.id}", horizon)
            competitor_spans.append(span)

    location_spans = []
    for loc in req.locations:
        loc_starts = []
        loc_ends = []

        for e in req.entries:
            ev = event_by_id[e.eventId]
            if loc.id in ev.possibleLocations:
                is_here = model.NewBoolVar(f"loc_{loc.id}_has_entry_{e.id}")
                model.Add(event_location[e.eventId] == loc.id).OnlyEnforceIf(is_here)
                model.Add(event_location[e.eventId] != loc.id).OnlyEnforceIf(is_here.Not())

                s = model.NewIntVar(0, horizon, f"loc_{loc.id}_s_{e.id}")
                f = model.NewIntVar(0, horizon, f"loc_{loc.id}_f_{e.id}")

                model.Add(s == start[e.id]).OnlyEnforceIf(is_here)
                model.Add(f == end[e.id]).OnlyEnforceIf(is_here)
                model.Add(s == horizon).OnlyEnforceIf(is_here.Not())
                model.Add(f == 0).OnlyEnforceIf(is_here.Not())

                loc_starts.append(s)
                loc_ends.append(f)

        if len(loc_starts) > 1:
            span = add_span(model, loc_starts, loc_ends, f"location_{loc.id}", horizon)
            location_spans.append(span)

    # ---------------- Objective: minimize makespan & global end ----------------
    global_start = model.NewIntVar(0, horizon, "global_start")
    global_end = model.NewIntVar(0, horizon, "global_end")
    global_makespan = model.NewIntVar(0, horizon, "global_makespan")

    model.AddMinEquality(global_start, list(start.values()))
    model.AddMaxEquality(global_end, list(end.values()))
    model.Add(global_makespan == global_end - global_start)

    total_span_ub = horizon * (1 + len(event_spans) + len(competitor_spans)
                                + len(location_spans) + len(group_event_spans))
    total_span = model.NewIntVar(0, total_span_ub, "total_span")

    model.Add(
        total_span == global_makespan
        + sum(event_spans) * req.typeWeight
        + sum(competitor_spans) * req.compWeight
        + sum(location_spans) * req.locWeight
        + sum(group_event_spans) * req.groupWeight
        + global_end
    )
    model.Minimize(total_span)

    # ---------------- Solve ----------------
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = 60 * 60
    solver.parameters.num_search_workers = 4
    solver.parameters.log_search_progress = True
    container["solver"] = solver

    status = solver.Solve(model, printer)
    printer.send_final_solution(status)
    return {"status": str(status)}