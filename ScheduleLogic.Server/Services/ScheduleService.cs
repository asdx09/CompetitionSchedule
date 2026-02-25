using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using ScheduleLogic.Server.Controllers;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using ScheduleLogic.Server.Class;
using static ScheduleLogic.Server.Class.EventModels;
using static ScheduleLogic.Server.Class.ScheduleModels;

namespace ScheduleLogic.Server.Services
{
    public class ScheduleService
    {
        private readonly DatabaseService _dbService;
        string apiUrl = "http://127.0.0.1:8000/";

        public ScheduleService(DatabaseService dbService)
        {
            _dbService = dbService;
        }
        public async Task<ScheduleRequestForSolver> GenerateSchedule(int id)
        {
            var SR = _dbService.GetScheduleInfo(id);
            using var client = new HttpClient();

            var httpResponse = await client.PostAsJsonAsync(apiUrl + "schedule", SR);

            if (httpResponse.IsSuccessStatusCode)
            {
                var jsonString = await httpResponse.Content.ReadAsStringAsync();
            }
            else
            {
                var error = await httpResponse.Content.ReadAsStringAsync();
                Console.WriteLine(error);
            }
            return SR;
        }

        public async Task<DataDTO> GetScheduleData(string id)
        {
            return _dbService.GetScheduleData(id);
        }

        public async Task<bool> CheckSolver(string id)
        {
            using var client = new HttpClient();

            var response = await client.GetAsync(apiUrl + "is_solver_running?EventId=" + id);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SolverStatusResponse>(jsonString);

                return result?.Running ?? false;
            }
            else
            {
                Console.WriteLine("API call error: " + response.StatusCode);
                return false;
            }
        }

        public async Task<bool> StopSolver(string id)
        {
            using var client = new HttpClient();

            var response = await client.GetAsync(apiUrl + "stop_solver?EventId=" + id);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<StopSolverResponse>(jsonString);

                return result?.Status == "STOPPED";
            }
            else
            {
                Console.WriteLine("API call error: " + response.StatusCode);
                return false;
            }
        }

        public async Task<byte[]> GetScheduleFile(string id)
        {
            var data = _dbService.GetScheduleDataEXPORT(id);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Schedule");

            worksheet.Cell(1, 1).Value = "Competitor";
            worksheet.Cell(1, 2).Value = "Event Type";
            worksheet.Cell(1, 3).Value = "Location";
            worksheet.Cell(1, 4).Value = "Slot";
            worksheet.Cell(1, 5).Value = "Start";
            worksheet.Cell(1, 6).Value = "End";
            worksheet.Range(1, 1, 1, 6).Style.Fill.BackgroundColor = XLColor.LightBlue;
            var sortedTimeZones = data.TimeZones
                .OrderBy(tz => tz.GroupName + tz.Participant)
                .ToList();
            int row = 2;
            string lastGroup = "-";
            foreach (var timezone in sortedTimeZones)
            {
                if (timezone.GroupName != lastGroup)
                {
                    lastGroup = timezone.GroupName;
                    if (timezone.GroupName != "") worksheet.Cell(row, 1).Value = timezone.GroupName + ":";
                    else worksheet.Cell(row, 1).Value = "Without group:";
                    worksheet.Range(row, 1, row, 6).Merge();
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightCoral;
                    row++;
                }
                worksheet.Cell(row, 1).Value = timezone.Participant;
                worksheet.Cell(row, 2).Value = timezone.EventType;
                worksheet.Cell(row, 3).Value = timezone.Location;
                worksheet.Cell(row, 4).Value = timezone.Slot;
                worksheet.Cell(row, 5).Value = timezone.StartTime.ToString();
                worksheet.Cell(row, 6).Value = timezone.EndTime.ToString();

                if(row % 2 == 0) worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightBlue;
                else worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightCyan;
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
