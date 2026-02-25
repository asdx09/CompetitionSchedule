import threading
import time
from concurrent.futures import ThreadPoolExecutor
import ssl
import aiohttp
import uvicorn
from fastapi import FastAPI
from ortools.sat.python import cp_model
import os
from fastapi import FastAPI
from Solver import schedule
from models import ScheduleRequestForSolver
import asyncio

CPU_COUNT = os.cpu_count()
running_solvers = {} 
running_solvers_lock = threading.Lock()


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

class DebounceSolutionPrinter(cp_model.CpSolverSolutionCallback):
    def __init__(self, entries, start_vars, end_vars, event_location_vars, return_url, eventId, debounce_sec=5):
        super().__init__()
        self.entries = entries
        self.start_vars = start_vars
        self.end_vars = end_vars
        self.event_location_vars = event_location_vars
        self.return_url = return_url
        self.eventId = eventId
        self.debounce_sec = debounce_sec
        self.solution_count = 0
        self.last_solution = None
        self.last_update_time = time.time()
        self.last_sendTime = 0
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
                    "ParticipantId": e.competitorId,
                    "EventTypeId": e.eventId,
                    "Start": self.Value(self.start_vars[e.id]),
                    "End": self.Value(self.end_vars[e.id]),
                    "LocationId": self.Value(self.event_location_vars[e.eventId]),
                    "Slot": 0
                }
                for e in self.entries
            ]

            self.last_update_time = time.time()

    def _debounce_loop(self):
        while not self._stop:
            time.sleep(0.5)
            now = time.time()
            with self._lock:
                if self.last_solution and (now - self.last_sendTime >= self.debounce_sec):
                    if self.last_update_time > self.last_sendTime:
                        self.last_sendTime = now
                        sol_copy = self.last_solution.copy()
                        sol_no = self.solution_count
                        threading.Thread(
                            target=self._send_partial_solution_thread,
                            args=(sol_copy, sol_no),
                            daemon=True
                        ).start()

    def _send_partial_solution_thread(self, solution, solution_number):
        asyncio.run(send_callback(self.return_url, {
            "Status": "PARTIAL_SOLUTION",
            "EventId": self.eventId,
            "SolutionNumber": str(solution_number),
            "Schedule": solution
        }))
        print(f"Partial solution sent #{solution_number}")

    def send_final_solution(self, solver_status):
        self._stop = True
        final_solution = self.last_solution
        if final_solution:
            asyncio.run(send_callback(self.return_url, {
                "Status": str(solver_status),
                "EventId": self.eventId,
                "solutionNumber": str(self.solution_count),
                "Schedule": final_solution
            }))
            print("Final solution sent.")

# -------------------- RUN SOLVER BACKGROUND --------------------
def run_solver_background(req: ScheduleRequestForSolver):
    printer = DebounceSolutionPrinter(req.entries, {}, {}, {}, req.returnURL, req.eventId)
    container = {"printer": printer, "solver": None}
    with running_solvers_lock:
        running_solvers[req.eventId] = container
    try:
        schedule(req, printer, container)
    finally:
        with running_solvers_lock:
            running_solvers.pop(req.eventId, None)


# -------------------- STOP SOLVER --------------------
def stop_solver_by_eventId(eventId: int) -> bool:
    with running_solvers_lock:
        container = running_solvers.get(eventId)
        if not container:
            return False
        printer = container["printer"]
        solver = container["solver"]
        printer._stop = True
        if solver:
            solver.StopSearch()
        return True


# -------------------- CHECK SOLVER --------------------
def is_solver_running(eventId: int) -> bool:
    with running_solvers_lock:
        return eventId in running_solvers


# -------------------- API ENDPOINTS --------------------
@app.post("/schedule")
async def schedule_endpoint(req: ScheduleRequestForSolver):
    eventId = req.eventId
    if is_solver_running(eventId):
        return {"status": "ALREADY_RUNNING", "eventId": eventId}
    loop = asyncio.get_running_loop()
    loop.run_in_executor(executor, run_solver_background, req)
    return {"Status": "SCHEDULE_STARTED", "EventId": eventId, "message": "Solver running in background."}

@app.get("/stop_solver")
async def stop_solver_endpoint(EventId: int):
    stopped = stop_solver_by_eventId(EventId)
    return {"status": "STOPPED" if stopped else "NOT_FOUND", "eventId": EventId}

@app.get("/is_solver_running")
async def is_solver_running_endpoint(EventId: int):
    running = is_solver_running(EventId)
    return {"Running": running, "EventId": EventId}

# -------------------- START SERVER --------------------
if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
