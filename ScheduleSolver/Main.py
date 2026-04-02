import threading
import time
import psutil
import os
import multiprocessing
import ssl
import aiohttp
import uvicorn
import asyncio
from fastapi import FastAPI
from ortools.sat.python import cp_model
from Solver import schedule
from models import ScheduleRequestForSolver

app = FastAPI()
active_processes = {}
processes_lock = threading.Lock()

MAX_PARALLEL = 1 
pending_queue = asyncio.Queue()  
running_count = 0
queue_lock = threading.Lock()

# -------------------- CALLBACK --------------------
async def send_callback(url, data):
    ssl_context = ssl.create_default_context()
    ssl_context.check_hostname = False
    ssl_context.verify_mode = ssl.CERT_NONE
    print("'" + url + "' sending callback...")
    try:
        async with aiohttp.ClientSession() as session:
            async with session.post(url, json=data, ssl=ssl_context, timeout=10) as resp:
                return await resp.text()
    except Exception as e:
        print(f"Callback error: {e}")

# -------------------- PRINTER --------------------
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
        if self._stop: return
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
                } for e in self.entries
            ]
            self.last_update_time = time.time()

    def _debounce_loop(self):
        while not self._stop:
            time.sleep(1)
            now = time.time()
            with self._lock:
                if self.last_solution and (now - self.last_sendTime >= self.debounce_sec):
                    if self.last_update_time > self.last_sendTime:
                        self.last_sendTime = now
                        sol_copy = self.last_solution.copy()
                        sol_no = self.solution_count
                        threading.Thread(target=self._send_partial, args=(sol_copy, sol_no), daemon=True).start()

    def _send_partial(self, solution, solution_number):
        asyncio.run(send_callback(self.return_url, {
            "Status": "PARTIAL_SOLUTION",
            "EventId": self.eventId,
            "SolutionNumber": str(solution_number),
            "Schedule": solution
        }))

    def send_final_solution(self, solver_status):
        self._stop = True
        if self.last_solution:
            asyncio.run(send_callback(self.return_url, {
                "Status": solver_status.name,
                "EventId": self.eventId,
                "solutionNumber": str(self.solution_count),
                "Schedule": self.last_solution
            }))

# -------------------- WATCHDOG --------------------
def memory_watchdog(process, event_id, limit_mb=500):
    try:
        while process.is_alive() and process.pid is None:
            time.sleep(0.1)
        
        if not process.is_alive(): return
        
        p_info = psutil.Process(process.pid)
        while process.is_alive():
            mem_mb = p_info.memory_info().rss / (1024 * 1024)
            if mem_mb > limit_mb:
                print(f"--- MEMORY LIMIT ({mem_mb:.1f} MB). Killing Event {event_id} ---")
                process.kill() 
                return
            time.sleep(2)
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        pass
    finally:
        with processes_lock:
            active_processes.pop(event_id, None)

# -------------------- SOLVER WORKER  --------------------
def run_solver_isolated(req: ScheduleRequestForSolver):
    container = {"solver": None}
    printer = DebounceSolutionPrinter(
        req.entries, {}, {}, {}, req.returnURL, req.eventId
    )
    
    try:
        schedule(req, printer, container)
    except Exception as e:
        print(f"Hiba a solverben: {e}")

# -------------------- QUEUE  --------------------
def try_start_next():
    global running_count
    with queue_lock:
        if running_count >= MAX_PARALLEL:
            return 
        if pending_queue.empty():
            return
        req = pending_queue.get_nowait()
        running_count += 1

    p = multiprocessing.Process(target=run_solver_isolated, args=(req,))
    p.start()
    
    with processes_lock:
        active_processes[req.eventId] = p

    threading.Thread(target=memory_watchdog, args=(p, req.eventId, 500), daemon=True).start()

    def monitor():
        nonlocal p
        p.join()
        global running_count
        with queue_lock:
            running_count -= 1
        try_start_next()  

    threading.Thread(target=monitor, daemon=True).start()

# -------------------- API ENDPOINTS --------------------
@app.post("/schedule")
async def schedule_endpoint(req: ScheduleRequestForSolver):
    with processes_lock:
        if req.eventId in active_processes and active_processes[req.eventId].is_alive():
            return {"status": "ALREADY_RUNNING", "eventId": req.eventId}

    await pending_queue.put(req)
    try_start_next()

    return {"Status": "QUEUED_OR_STARTED", "EventId": req.eventId}

@app.get("/stop_solver")
async def stop_solver_endpoint(EventId: int):
    stopped = False

    with processes_lock:
        p = active_processes.get(EventId)
        if p and p.is_alive():
            p.kill()
            active_processes.pop(EventId, None)
            stopped = True

    with queue_lock:
        new_queue = asyncio.Queue()
        while not pending_queue.empty():
            req = pending_queue.get_nowait()
            if req.eventId != EventId:
                await new_queue.put(req)
            else:
                stopped = True
        pending_queue._queue = new_queue._queue

    if stopped:
        return {"status": "STOPPED", "eventId": EventId}
    else:
        return {"status": "NOT_RUNNING", "eventId": EventId}

@app.get("/is_solver_running")
async def is_solver_running_endpoint(EventId: int):
    with processes_lock, queue_lock:
        running = (
            (EventId in active_processes and active_processes[EventId].is_alive()) or
            any(req.eventId == EventId for req in pending_queue._queue)
        )
    return {"Running": running, "EventId": EventId}

# -------------------- START --------------------
if __name__ == "__main__":
    multiprocessing.freeze_support()
    uvicorn.run(app, host="0.0.0.0", port=8000)