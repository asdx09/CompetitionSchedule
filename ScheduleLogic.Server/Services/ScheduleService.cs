using ScheduleLogic.Server.Services.Interfaces;
using ScheduleLogic.Server.Services.Security;
using System.Text.Json;
using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly IScheduleRepository _schedules;
        private readonly IEventRepository _events;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ScheduleService> _logger;
        private readonly SolverCallbackSigner _signer;

        // The solver's JSON keys are not consistently cased.
        private static readonly JsonSerializerOptions SolverJson = new() { PropertyNameCaseInsensitive = true };

        public ScheduleService(
            IScheduleRepository schedules,
            IEventRepository events,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ScheduleService> logger,
            SolverCallbackSigner signer)
        {
            _schedules = schedules;
            _events = events;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _signer = signer;
        }

        public async Task<ScheduleRequestForSolver?> GenerateSchedule(long eventId, long userId)
        {
            if (!await _events.IsOwnedByUserAsync(eventId, userId))
                return null;

            var request = await _schedules.GetScheduleInfoAsync(eventId);

            var backendForSolver = _configuration["URLS:BackendForSolver"];
            if (string.IsNullOrWhiteSpace(backendForSolver)) backendForSolver = _configuration["URLS:Backend"]!;
            // The callback URL carries a per-event HMAC key; the answer endpoint only accepts that.
            request.ReturnURL =
                backendForSolver.TrimEnd('/') +
                "/api/Schedule/answer?key=" + _signer.KeyFor(eventId);

            var client = _httpClientFactory.CreateClient("Solver");

            var response = await client.PostAsJsonAsync("schedule", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                _logger.LogWarning(
                    "Solver rejected the schedule request for event {EventId}: {Error}",
                    eventId,
                    error);
            }

            request.ReturnURL = "";
            return request;
        }

        public async Task<DataDTO?> GetScheduleData(long eventId, long userId)
        {
            if (!await _events.IsOwnedByUserAsync(eventId, userId)) return null;
            return await _schedules.GetScheduleDataAsync(eventId);
        }

        public async Task<bool?> CheckSolver(long eventId, long userId)
        {
            if (!await _events.IsOwnedByUserAsync(eventId, userId)) return null;

            var client = _httpClientFactory.CreateClient("Solver");
            var response = await client.GetAsync($"is_solver_running?EventId={eventId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Solver status check failed for event {EventId}: {Status}", eventId, response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SolverStatusResponse>(json, SolverJson);
            return result?.Running ?? false;
        }

        public async Task<bool?> StopSolver(long eventId, long userId)
        {
            if (!await _events.IsOwnedByUserAsync(eventId, userId)) return null;

            var client = _httpClientFactory.CreateClient("Solver");
            var response = await client.GetAsync($"stop_solver?EventId={eventId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Solver stop failed for event {EventId}: {Status}", eventId, response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<StopSolverResponse>(json, SolverJson);
            return result?.Status == "STOPPED";
        }

        public async Task<bool> ReceiveSolverAnswer(SolverResponse data)
        {
            bool hasSolution = data.Status is "FEASIBLE" or "PARTIAL_SOLUTION" or "OPTIMAL";
            if (!hasSolution || data.Schedule == null)
            {
                _logger.LogInformation("No solution for event {EventId}, solver status: {Status}", data.EventId, data.Status);
                return true;
            }

            bool saved = await _schedules.SaveSolverResultAsync(data.Schedule, data.EventId);
            if (!saved)
                _logger.LogWarning("Solver answer for event {EventId} references foreign objects - rejected.", data.EventId);
            return saved;
        }

        public async Task<byte[]?> GetScheduleFile(long eventId, long userId)
        {
            if (!await _events.IsOwnedByUserAsync(eventId, userId)) return null;

            var data = await _schedules.GetExportDataAsync(eventId);
            if (data == null) return null;

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Schedule");

            worksheet.Cell(1, 1).Value = "Competitor";
            worksheet.Cell(1, 2).Value = "Group";
            worksheet.Cell(1, 3).Value = "Event Type";
            worksheet.Cell(1, 4).Value = "Location";
            worksheet.Cell(1, 5).Value = "Slot";
            worksheet.Cell(1, 6).Value = "Start";
            worksheet.Cell(1, 7).Value = "End";
            worksheet.Range(1, 1, 1, 7).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;

            var sortedTimeZones = data.TimeZones
                .OrderBy(tz => tz.GroupName + tz.Participant)
                .ToList();

            int row = 2;
            foreach (var tz in sortedTimeZones)
            {
                worksheet.Cell(row, 1).SetValue(tz.Participant);
                worksheet.Cell(row, 2).SetValue(tz.GroupName);
                worksheet.Cell(row, 3).SetValue(tz.EventType);
                worksheet.Cell(row, 4).SetValue(tz.Location);
                worksheet.Cell(row, 5).Value = tz.Slot;
                worksheet.Cell(row, 6).SetValue(tz.StartTime.ToString());
                worksheet.Cell(row, 7).SetValue(tz.EndTime.ToString());

                var fill = row % 2 == 0 ? ClosedXML.Excel.XLColor.LightBlue : ClosedXML.Excel.XLColor.LightCyan;
                worksheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = fill;
                row++;
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}