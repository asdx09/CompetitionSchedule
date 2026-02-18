import asyncio
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from typing import List
import ssl
import aiohttp
import uvicorn
from fastapi import FastAPI
from pydantic import BaseModel
from ortools.sat.python import cp_model
import os

CPU_COUNT = os.cpu_count()

# -------------------- MODELS --------------------
class LocationModel(BaseModel):
    id: int
    name: str
    capacity: int
class EventModel(BaseModel):
    id: int
    name: str
    duration: int
    possible_locations: List[int]
class CompetitorModel(BaseModel):
    id: int
    name: str
    group_id: int = 0
class EntryModel(BaseModel):
    id: int
    competitor_id: int
    event_id: int
class ConstraintModel(BaseModel):
    id: int
    object_id: int
    constraintType: str  # 'L', 'C', 'E', 'G'
    startTime: int
    endTime: int
class PauseTableModel(BaseModel):
    id: int
    locationId1: int
    locationId2: int
    pause: int
class ScheduleRequestForSolver(BaseModel):
    returnURL: str
    event_id: int
    locations: List[LocationModel]
    events: List[EventModel]
    competitors: List[CompetitorModel]
    entries: List[EntryModel]
    travel: List[PauseTableModel]
    constraints: List[ConstraintModel]
    day_length: int
    max_days: int
    break_time_loc: int
    base_pause_time: int
    locWeight: int
    groupWeight: int
    typeWeight: int
    compWeight: int

# -------------------- HELPER --------------------
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

# -------------------- FASTAPI --------------------
app = FastAPI()
executor = ThreadPoolExecutor(max_workers=1)

# -------------------- CALLBACK --------------------
async def send_callback(url, data):
    ssl_context = ssl.create_default_context()
    ssl_context.check_hostname = False
    ssl_context.verify_mode = ssl.CERT_NONE
    async with aiohttp.ClientSession() as session:
        async with session.post(url, json=data, ssl=ssl_context) as resp:
            return await resp.text()

# -------------------- RUNNING SOLVERS --------------------
running_solvers = {}  # event_id -> {printer, solver}
running_solvers_lock = threading.Lock()

# -------------------- SOLUTION CALLBACK --------------------
class DebounceSolutionPrinter(cp_model.CpSolverSolutionCallback):
    def __init__(self, entries, start_vars, end_vars, event_location_vars, return_url, event_id, debounce_sec=5):
        super().__init__()
        self.entries = entries
        self.start_vars = start_vars
        self.end_vars = end_vars
        self.event_location_vars = event_location_vars
        self.return_url = return_url
        self.event_id = event_id
        self.debounce_sec = debounce_sec
        self.solution_count = 0
        self.last_solution = None
        self.last_update_time = time.time()
        self.last_send_time = 0
        self._lock = threading.Lock()
        self._stop = False

        self._thread = threading.Thread(target=self._debounce_loop, daemon=True)
        self._thread.start()

    def OnSolutionCallback(self):
        if self._stop:
            return
        self.solution_count += 1
        with self._lock:
            self.last_solution = [
                {
                    "participant_id": e.competitor_id,
                    "eventtype_id": e.event_id,
                    "start": self.Value(self.start_vars[e.id]),
                    "end": self.Value(self.end_vars[e.id]),
                    "location_id": self.Value(self.event_location_vars[e.event_id]),
                }
                for e in self.entries
            ]

            self.last_update_time = time.time()

    def _debounce_loop(self):
        while not self._stop:
            time.sleep(0.5)
            now = time.time()
            with self._lock:
                if self.last_solution and (now - self.last_send_time >= self.debounce_sec):
                    if self.last_update_time > self.last_send_time:
                        self.last_send_time = now
                        sol_copy = self.last_solution.copy()
                        sol_no = self.solution_count
                        threading.Thread(
                            target=self._send_partial_solution_thread,
                            args=(sol_copy, sol_no),
                            daemon=True
                        ).start()

    def _send_partial_solution_thread(self, solution, solution_number):
        asyncio.run(send_callback(self.return_url, {
            "status": "PARTIAL_SOLUTION",
            "event_id": self.event_id,
            "solution_number": str(solution_number),
            "schedule": solution
        }))
        print(f"Partial solution sent #{solution_number}")

    def send_final_solution(self, solver_status):
        self._stop = True
        final_solution = self.last_solution
        if final_solution:
            asyncio.run(send_callback(self.return_url, {
                "status": str(solver_status),
                "event_id": self.event_id,
                "solution_number": str(self.solution_count),
                "schedule": final_solution
            }))
            print("Final solution sent.")

# -------------------- SOLVER --------------------
def schedule(req: ScheduleRequestForSolver, printer, container):
    print("Solver started!")
    horizon = req.day_length * req.max_days
    model = cp_model.CpModel()

    loc_by_id = {l.id: l for l in req.locations}
    comp_by_id = {c.id: c for c in req.competitors}
    event_by_id = {e.id: e for e in req.events}

    start, end, interval = {}, {}, {}
    event_location = {}

    # ---------------- Event location ----------------
    for ev in req.events:
        event_location[ev.id] = model.NewIntVarFromDomain(
            cp_model.Domain.FromValues(ev.possible_locations),
            f"event_loc_{ev.id}"
        )

    # ---------------- Entry intervals ----------------
    for e in req.entries:
        ev = event_by_id[e.event_id]
        start[e.id] = model.NewIntVar(0, horizon - ev.duration, f"start_{e.id}")
        end[e.id] = model.NewIntVar(ev.duration, horizon, f"end_{e.id}")
        interval[e.id] = model.NewIntervalVar(start[e.id], ev.duration, end[e.id], f"interval_{e.id}")

    printer.start_vars.update(start)
    printer.end_vars.update(end)
    printer.event_location_vars.update(event_location)

    # ---------------- Location capacity + break_time ----------------
    for loc in req.locations:
        loc_intervals = []
        for e in req.entries:
            ev = event_by_id[e.event_id]
            if loc.id in ev.possible_locations:
                is_here = model.NewBoolVar(f"entry_{e.id}_on_loc_{loc.id}")
                opt_interval = model.NewOptionalIntervalVar(
                    start[e.id],
                    ev.duration + req.break_time_loc,
                    end[e.id] + req.break_time_loc,
                    is_here,
                    f"optint_{e.id}_loc{loc.id}"
                )
                model.Add(event_location[e.event_id] == loc.id).OnlyEnforceIf(is_here)
                model.Add(event_location[e.event_id] != loc.id).OnlyEnforceIf(is_here.Not())
                loc_intervals.append(opt_interval)
        if loc_intervals:
            model.AddCumulative(loc_intervals, [1]*len(loc_intervals), loc.capacity)

    # ---------------- Competitor + Travel constraints ----------------
    for comp in req.competitors:
        comp_entries = [e for e in req.entries if e.competitor_id == comp.id]
        comp_intervals = [interval[e.id] for e in comp_entries]
        if len(comp_intervals) > 1:
            model.AddNoOverlap(comp_intervals)

        for i in range(len(comp_entries)):
            for j in range(i + 1, len(comp_entries)):
                a = comp_entries[i]
                b = comp_entries[j]

                a_before_b = model.NewBoolVar(f"a{a.id}_before_b{b.id}")

                loc_a_var = event_location[a.event_id]
                loc_b_var = event_location[b.event_id]

                travel_expr = req.base_pause_time  

                for p in req.travel:
                    cond_a = model.NewBoolVar(f"cond_a_{a.id}_{b.id}_{p.locationId1}")
                    cond_b = model.NewBoolVar(f"cond_b_{a.id}_{b.id}_{p.locationId2}")
                    model.Add(loc_a_var == p.locationId1).OnlyEnforceIf(cond_a)
                    model.Add(loc_a_var != p.locationId1).OnlyEnforceIf(cond_a.Not())
                    model.Add(loc_b_var == p.locationId2).OnlyEnforceIf(cond_b)
                    model.Add(loc_b_var != p.locationId2).OnlyEnforceIf(cond_b.Not())

                    cond = model.NewBoolVar(f"travel_cond_{a.id}_{b.id}_{p.locationId1}_{p.locationId2}")
                    model.AddBoolAnd([cond_a, cond_b]).OnlyEnforceIf(cond)
                    model.AddBoolOr([cond_a.Not(), cond_b.Not()]).OnlyEnforceIf(cond.Not())

                    travel_expr += cond * (p.pause - req.base_pause_time)

                model.Add(start[b.id] >= end[a.id] + travel_expr).OnlyEnforceIf(a_before_b)
                model.Add(start[a.id] >= end[b.id] + travel_expr).OnlyEnforceIf(a_before_b.Not())

   # ---------------- Group-level single-location-at-a-time constraint ----------------
    groups = {}
    for comp in req.competitors:
        if comp.group_id != -1:
            groups.setdefault(comp.group_id, []).append(comp.id)

    for group_id, member_ids in groups.items():
        group_entries = [e for e in req.entries if comp_by_id[e.competitor_id].group_id == group_id]

        for i in range(len(group_entries)):
            for j in range(i + 1, len(group_entries)):
                e1 = group_entries[i]
                e2 = group_entries[j]

                # Bool: ugyanazon a helyen?
                same_loc = model.NewBoolVar(f"group{group_id}_entry{e1.id}_{e2.id}_same_loc")
                model.Add(event_location[e1.event_id] == event_location[e2.event_id]).OnlyEnforceIf(same_loc)
                model.Add(event_location[e1.event_id] != event_location[e2.event_id]).OnlyEnforceIf(same_loc.Not())

                # Sorrend változó, csak akkor kell, ha különböző helyen
                e1_before_e2 = model.NewBoolVar(f"e1_before_e2_{e1.id}_{e2.id}")

                # Travel linear expression
                travel_expr = req.base_pause_time
                for p in req.travel:
                    cond_a = model.NewBoolVar(f"cond_a_{e1.id}_{e2.id}_{p.locationId1}")
                    cond_b = model.NewBoolVar(f"cond_b_{e1.id}_{e2.id}_{p.locationId2}")

                    model.Add(event_location[e1.event_id] == p.locationId1).OnlyEnforceIf(cond_a)
                    model.Add(event_location[e1.event_id] != p.locationId1).OnlyEnforceIf(cond_a.Not())
                    model.Add(event_location[e2.event_id] == p.locationId2).OnlyEnforceIf(cond_b)
                    model.Add(event_location[e2.event_id] != p.locationId2).OnlyEnforceIf(cond_b.Not())

                    cond = model.NewBoolVar(f"travel_cond_{e1.id}_{e2.id}_{p.locationId1}_{p.locationId2}")
                    model.AddBoolAnd([cond_a, cond_b]).OnlyEnforceIf(cond)
                    model.AddBoolOr([cond_a.Not(), cond_b.Not()]).OnlyEnforceIf(cond.Not())

                    travel_expr += cond * (p.pause - req.base_pause_time)

                # Csak akkor kényszerítjük a sorrendet, ha különböző hely
                model.Add(start[e2.id] >= end[e1.id] + travel_expr).OnlyEnforceIf([e1_before_e2, same_loc.Not()])
                model.Add(start[e1.id] >= end[e2.id] + travel_expr).OnlyEnforceIf([e1_before_e2.Not(), same_loc.Not()])

                # Ha ugyanazon a helyen, nincs sorrend -> nincs extra constraint




    # ---------------- Constraints (tiltások) ----------------
    for cons in req.constraints:
        affected_entries = []
        if cons.constraintType == 'C':
            affected_entries = [e for e in req.entries if e.competitor_id == cons.object_id]
        elif cons.constraintType == 'L':
            for e in req.entries:
                is_on_loc = model.NewBoolVar(f"entry_{e.id}_on_loc_{cons.object_id}")

                model.Add(event_location[e.event_id] == cons.object_id)\
                     .OnlyEnforceIf(is_on_loc)
                model.Add(event_location[e.event_id] != cons.object_id)\
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
                    f"entry_{e.id}_is_eventtype_{cons.object_id}"
                )

                model.Add(event_by_id[e.event_id].id == cons.object_id)\
                     .OnlyEnforceIf(is_of_type)
                model.Add(event_by_id[e.event_id].id != cons.object_id)\
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
            group_members = [c.id for c in req.competitors if c.group_id == cons.object_id]
            affected_entries = [e for e in req.entries if e.competitor_id in group_members]

        for e in affected_entries:
            add_forbidden_interval(model, start[e.id], end[e.id], cons.startTime, cons.endTime, e.id)


    # ---------------- Group-level span ----------------
    # group_id -> event_id -> entry list
    group_event_entries = {}
    for comp in req.competitors:
        if comp.group_id != -1:
            group_id = comp.group_id
            group_event_entries.setdefault(group_id, {})
            for e in req.entries:
                if e.competitor_id == comp.id:
                    group_event_entries[group_id].setdefault(e.event_id, []).append(e.id)

    group_event_spans = []  # majd az objective-ben használjuk

    for group_id, events in group_event_entries.items():
        for event_id, entry_ids in events.items():
            if len(entry_ids) > 1:  # csak ha több tag van a csoportban
                starts = [start[eid] for eid in entry_ids]
                ends = [end[eid] for eid in entry_ids]

                min_start = model.NewIntVar(0, horizon, f"group{group_id}_event{event_id}_min_start")
                max_end = model.NewIntVar(0, horizon, f"group{group_id}_event{event_id}_max_end")
                span = model.NewIntVar(0, horizon, f"group{group_id}_event{event_id}_span")

                model.AddMinEquality(min_start, starts)
                model.AddMaxEquality(max_end, ends)
                model.Add(span == max_end - min_start)

                group_event_spans.append(span)



    event_spans = []

    for ev in req.events:
        ev_entries = [e for e in req.entries if e.event_id == ev.id]
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
        comp_entries = [e for e in req.entries if e.competitor_id == comp.id]
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
            ev = event_by_id[e.event_id]
            if loc.id in ev.possible_locations:
                is_here = model.NewBoolVar(f"loc_{loc.id}_has_entry_{e.id}")

                model.Add(event_location[e.event_id] == loc.id).OnlyEnforceIf(is_here)
                model.Add(event_location[e.event_id] != loc.id).OnlyEnforceIf(is_here.Not())

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

    
    #model.Minimize(global_makespan) 

    # ---------------- Solve ----------------
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = 60*60
    solver.parameters.num_search_workers = 4
    solver.parameters.log_search_progress = True
    container["solver"] = solver
    status = solver.Solve(model, solution_callback=printer)
    printer.send_final_solution(status)
    return {"status": str(status)}

# -------------------- RUN SOLVER BACKGROUND --------------------
def run_solver_background(req: ScheduleRequestForSolver):
    printer = DebounceSolutionPrinter(req.entries, {}, {}, {}, req.returnURL, req.event_id)
    container = {"printer": printer, "solver": None}
    with running_solvers_lock:
        running_solvers[req.event_id] = container
    try:
        schedule(req, printer, container)
    finally:
        with running_solvers_lock:
            running_solvers.pop(req.event_id, None)

# -------------------- STOP SOLVER --------------------
def stop_solver_by_event_id(event_id: int) -> bool:
    with running_solvers_lock:
        container = running_solvers.get(event_id)
        if not container:
            return False
        printer = container["printer"]
        solver = container["solver"]
        printer._stop = True
        if solver:
            solver.StopSearch()
        return True

# -------------------- CHECK SOLVER --------------------
def is_solver_running(event_id: int) -> bool:
    with running_solvers_lock:
        return event_id in running_solvers

# -------------------- API ENDPOINTS --------------------
@app.post("/schedule")
async def schedule_endpoint(req: ScheduleRequestForSolver):
    event_id = req.event_id
    if is_solver_running(event_id):
        return {"status": "ALREADY_RUNNING", "event_id": event_id}
    loop = asyncio.get_running_loop()
    loop.run_in_executor(executor, run_solver_background, req)
    return {"status": "SCHEDULE_STARTED", "event_id": event_id, "message": "Solver running in background."}

@app.get("/stop_solver")
async def stop_solver_endpoint(event_id: int):
    stopped = stop_solver_by_event_id(event_id)
    return {"status": "STOPPED" if stopped else "NOT_FOUND", "event_id": event_id}

@app.get("/is_solver_running")
async def is_solver_running_endpoint(event_id: int):
    running = is_solver_running(event_id)
    return {"running": running, "event_id": event_id}

# -------------------- START SERVER --------------------
if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
