from ortools.sat.python import cp_model
import threading, time, asyncio
from models import ScheduleRequestForSolver

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


def schedule(req: ScheduleRequestForSolver, printer, container):
    print("Solver started!")
    horizon = req.dayLength * req.maxDays
    model = cp_model.CpModel()

    loc_by_id = {l.id: l for l in req.locations}
    comp_by_id = {c.id: c for c in req.competitors}
    event_by_id = {e.id: e for e in req.events}

    start, end, interval = {}, {}, {}
    event_location = {}

    # ---------------- Event location ----------------
    for ev in req.events:
        event_location[ev.id] = model.NewIntVarFromDomain(
            cp_model.Domain.FromValues(ev.possibleLocations),
            f"event_loc_{ev.id}"
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

    for comp in req.competitors:
        comp_entries = [e for e in req.entries if e.competitorId == comp.id]
        intervals = []
        for i, e in enumerate(comp_entries):
            ev = event_by_id[e.eventId]
            start_var = start[e.id]
            end_var = end[e.id]

            iv = model.NewIntervalVar(start_var, ev.duration, end_var, f"comp_{comp.id}_entry_{e.id}")
            intervals.append(iv)

        if len(intervals) > 1:
            model.AddNoOverlap(intervals)

        for i in range(len(comp_entries)):
            for j in range(i + 1, len(comp_entries)):
                e1 = comp_entries[i]
                e2 = comp_entries[j]

                travel_time = req.basePauseTime
                for t in req.travel:
                    cond1 = model.NewBoolVar(f"travel_cond1_{e1.id}_{e2.id}_{t.id}")
                    cond2 = model.NewBoolVar(f"travel_cond2_{e1.id}_{e2.id}_{t.id}")
                    model.Add(event_location[e1.eventId] == t.locationID1).OnlyEnforceIf(cond1)
                    model.Add(event_location[e1.eventId] != t.locationID1).OnlyEnforceIf(cond1.Not())
                    model.Add(event_location[e2.eventId] == t.locationID2).OnlyEnforceIf(cond2)
                    model.Add(event_location[e2.eventId] != t.locationID2).OnlyEnforceIf(cond2.Not())
                    cond = model.NewBoolVar(f"travel_cond_{e1.id}_{e2.id}_{t.id}")
                    model.AddBoolAnd([cond1, cond2]).OnlyEnforceIf(cond)
                    model.AddBoolOr([cond1.Not(), cond2.Not()]).OnlyEnforceIf(cond.Not())
                    travel_time += cond * (t.pause - req.basePauseTime)

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
            model.AddCumulative(loc_intervals, [1]*len(loc_intervals), loc.capacity)

   # ---------------- Wave start per location (same event type per batch) ----------------

    for loc in req.locations:

        loc_entries = [
            e for e in req.entries
            if loc.id in event_by_id[e.eventId].possibleLocations
        ]

        if len(loc_entries) < 2:
            continue

        for i in range(len(loc_entries)):
            for j in range(i + 1, len(loc_entries)):

                e1 = loc_entries[i]
                e2 = loc_entries[j]

                e1_here = model.NewBoolVar(f"e{e1.id}_on_loc{loc.id}")
                e2_here = model.NewBoolVar(f"e{e2.id}_on_loc{loc.id}")

                model.Add(event_location[e1.eventId] == loc.id).OnlyEnforceIf(e1_here)
                model.Add(event_location[e1.eventId] != loc.id).OnlyEnforceIf(e1_here.Not())

                model.Add(event_location[e2.eventId] == loc.id).OnlyEnforceIf(e2_here)
                model.Add(event_location[e2.eventId] != loc.id).OnlyEnforceIf(e2_here.Not())

                both_here = model.NewBoolVar(
                    f"both_e{e1.id}_{e2.id}_loc{loc.id}"
                )

                model.AddBoolAnd([e1_here, e2_here]).OnlyEnforceIf(both_here)
                model.AddBoolOr([e1_here.Not(), e2_here.Not()]).OnlyEnforceIf(both_here.Not())

                same_start = model.NewBoolVar(
                    f"same_start_{e1.id}_{e2.id}_loc{loc.id}"
                )

                model.Add(start[e1.id] == start[e2.id])\
                    .OnlyEnforceIf([same_start, both_here])

                model.Add(start[e1.id] != start[e2.id])\
                    .OnlyEnforceIf([same_start.Not(), both_here])


                if e1.eventId != e2.eventId:
                    model.Add(same_start == 0).OnlyEnforceIf(both_here)

                e1_before_e2 = model.NewBoolVar(
                    f"e{e1.id}_before_e{e2.id}_loc{loc.id}"
                )

                model.Add(end[e1.id] <= start[e2.id])\
                    .OnlyEnforceIf([e1_before_e2, same_start.Not(), both_here])

                model.Add(end[e2.id] <= start[e1.id])\
                    .OnlyEnforceIf([e1_before_e2.Not(), same_start.Not(), both_here])

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

                e1_before_e2 = model.NewBoolVar(f"e1_before_e2_{e1.id}_{e2.id}")

                # Travel linear expression
                travel_expr = req.basePauseTime
                for p in req.travel:
                    cond_a = model.NewBoolVar(f"cond_a_{e1.id}_{e2.id}_{p.locationID1}")
                    cond_b = model.NewBoolVar(f"cond_b_{e1.id}_{e2.id}_{p.locationID2}")

                    model.Add(event_location[e1.eventId] == p.locationID1).OnlyEnforceIf(cond_a)
                    model.Add(event_location[e1.eventId] != p.locationID1).OnlyEnforceIf(cond_a.Not())
                    model.Add(event_location[e2.eventId] == p.locationID2).OnlyEnforceIf(cond_b)
                    model.Add(event_location[e2.eventId] != p.locationID2).OnlyEnforceIf(cond_b.Not())

                    cond = model.NewBoolVar(f"travel_cond_{e1.id}_{e2.id}_{p.locationID1}_{p.locationID2}")
                    model.AddBoolAnd([cond_a, cond_b]).OnlyEnforceIf(cond)
                    model.AddBoolOr([cond_a.Not(), cond_b.Not()]).OnlyEnforceIf(cond.Not())

                    travel_expr += cond * (p.pause - req.basePauseTime)

                model.Add(start[e2.id] >= end[e1.id] + travel_expr).OnlyEnforceIf([e1_before_e2, same_loc.Not()])
                model.Add(start[e1.id] >= end[e2.id] + travel_expr).OnlyEnforceIf([e1_before_e2.Not(), same_loc.Not()])





    # ---------------- Constraints ----------------
    for cons in req.constraints:
        affected_entries = []
        if cons.constraintType == 'C':
            affected_entries = [e for e in req.entries if e.competitorId == cons.objectId]
        elif cons.constraintType == 'L':
            for e in req.entries:
                is_on_loc = model.NewBoolVar(f"entry_{e.id}_on_loc_{cons.objectId}")

                model.Add(event_location[e.eventId] == cons.objectId)\
                     .OnlyEnforceIf(is_on_loc)
                model.Add(event_location[e.eventId] != cons.objectId)\
                     .OnlyEnforceIf(is_on_loc.Not())

                add_forbidden_interval(
                    model,
                    start[e.id],
                    end[e.id],
                    cons.startTime,
                    cons.endTime,
                    e.id,
                    enforce_if=is_on_loc
)
        elif cons.constraintType == 'T':
            for e in req.entries:
                is_of_type = model.NewBoolVar(
                    f"entry_{e.id}_is_eventtype_{cons.objectId}"
                )

                model.Add(event_by_id[e.eventId].id == cons.objectId)\
                     .OnlyEnforceIf(is_of_type)
                model.Add(event_by_id[e.eventId].id != cons.objectId)\
                     .OnlyEnforceIf(is_of_type.Not())

                add_forbidden_interval(
                    model,
                    start[e.id],
                    end[e.id],
                    cons.startTime,
                    cons.endTime,
                    e.id,
                    enforce_if=is_of_type
                )
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

                min_start = model.NewIntVar(0, horizon, f"group{groupId}_event{eventId}_min_start")
                max_end = model.NewIntVar(0, horizon, f"group{groupId}_event{eventId}_max_end")
                span = model.NewIntVar(0, horizon, f"group{groupId}_event{eventId}_span")

                model.AddMinEquality(min_start, starts)
                model.AddMaxEquality(max_end, ends)
                model.Add(span == max_end - min_start)

                group_event_spans.append(span)



    event_spans = []

    for ev in req.events:
        ev_entries = [e for e in req.entries if e.eventId == ev.id]
        if len(ev_entries) > 1:
            ev_starts = [start[e.id] for e in ev_entries]
            ev_ends = [end[e.id] for e in ev_entries]

            span = add_span(
                model,
                ev_starts,
                ev_ends,
                f"event_{ev.id}",
                horizon
            )
            event_spans.append(span)

    competitor_spans = []

    for comp in req.competitors:
        comp_entries = [e for e in req.entries if e.competitorId == comp.id]
        if len(comp_entries) > 1:
            comp_starts = [start[e.id] for e in comp_entries]
            comp_ends = [end[e.id] for e in comp_entries]

            span = add_span(
                model,
                comp_starts,
                comp_ends,
                f"competitor_{comp.id}",
                horizon
            )
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
            span = add_span(
                model,
                loc_starts,
                loc_ends,
                f"location_{loc.id}",
                horizon
            )
            location_spans.append(span)


    # ---------------- Objective: minimize makespan & global end ----------------
    BIG = 1_000_000
    global_start = model.NewIntVar(0, horizon, "global_start")
    global_end = model.NewIntVar(0, horizon, "global_end")
    global_makespan = model.NewIntVar(0, horizon, "global_makespan")

    model.AddMinEquality(global_start, list(start.values()))
    model.AddMaxEquality(global_end, list(end.values()))
    model.Add(global_makespan == global_end - global_start)

    max_total = (
        horizon + 
        horizon * len(event_spans) * req.typeWeight +
        horizon * len(competitor_spans) * req.compWeight +
        horizon * len(location_spans) * req.locWeight +
        horizon * len(group_event_spans) * req.groupWeight +
        horizon  
    )

    total_span = model.NewIntVar(0, horizon * (1 + len(event_spans) + len(competitor_spans) + len(location_spans) + len(group_event_spans)),
                             "total_span")


    model.Add(total_span == global_makespan + sum(event_spans)*req.typeWeight + sum(competitor_spans)*req.compWeight + sum(location_spans)*req.locWeight + sum(group_event_spans)*req.groupWeight + global_end)
    model.Minimize(total_span)


    # ---------------- Solve ----------------
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = 60*60*3
    solver.parameters.num_search_workers = 4
    solver.parameters.log_search_progress = False
    container["solver"] = solver
    cb = printer;
    status = solver.Solve(model,cb)
    printer.send_final_solution(status)
    return {"status": str(status)}