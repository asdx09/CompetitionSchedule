from pydantic import BaseModel
from typing import List

class LocationModel(BaseModel):
    id: int
    name: str
    capacity: int

class EventModel(BaseModel):
    id: int
    name: str
    duration: int
    possibleLocations: List[int]

class CompetitorModel(BaseModel):
    id: int
    name: str
    groupId: int = 0

class EntryModel(BaseModel):
    id: int
    competitorId: int
    eventId: int

class ConstraintModel(BaseModel):
    id: int
    objectId: int
    constraintType: str  # 'L', 'C', 'E', 'G'
    startTime: int
    endTime: int

class PauseTableModel(BaseModel):
    id: int
    locationID1: int
    locationID2: int
    pause: int

class ScheduleRequestForSolver(BaseModel):
    returnURL: str
    eventId: int
    locations: List[LocationModel]
    events: List[EventModel]
    competitors: List[CompetitorModel]
    entries: List[EntryModel]
    travel: List[PauseTableModel]
    constraints: List[ConstraintModel]
    dayLength: int
    maxDays: int
    breakTimeLoc: int
    basePauseTime: int
    locWeight: int
    groupWeight: int
    typeWeight: int
    compWeight: int