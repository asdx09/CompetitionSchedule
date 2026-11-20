using System.Diagnostics;
using ScheduleLogic.TestKit.Http;

namespace ScheduleLogic.TestKit.Testing;

public static class TestRunner
{
    public static async Task<TestResult> RunAsync(TestCase test, BackendTarget target, CancellationToken ct = default)
    {
        var result = new TestResult
        {
            TestId = test.Id,
            Group = test.Group,
            Name = test.Name,
            Status = TestStatus.Running,
            StartedAt = DateTime.Now,
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(test.Timeout);

        var sw = Stopwatch.StartNew();
        var ctx = new TestContext(target, timeout.Token);
        try
        {
            await test.Body(ctx);
            result.Status = TestStatus.Passed;
            result.Message = "OK";
        }
        catch (TestFailedException ex)
        {
            result.Status = TestStatus.Failed;
            result.Message = ex.Message;
        }
        catch (TestSkippedException ex)
        {
            result.Status = TestStatus.Skipped;
            result.Message = ex.Message;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            result.Status = TestStatus.Skipped;
            result.Message = "Cancelled.";
        }
        catch (OperationCanceledException)
        {
            result.Status = TestStatus.Failed;
            result.Message = $"Timeout ({test.Timeout.TotalSeconds:0} s).";
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.Message = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            await ctx.DisposeAsync();
            result.DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds);
            result.Log = ctx.Log.ToList();
            result.Exchanges = ctx.Exchanges.ToList();
        }
        return result;
    }

        public static async Task<List<TestResult>> RunManyAsync(
        IEnumerable<TestCase> tests,
        BackendTarget target,
        Action<TestCase>? onStarted = null,
        Action<TestResult>? onFinished = null,
        CancellationToken ct = default)
    {
        var results = new List<TestResult>();
        foreach (var test in tests)
        {
            if (ct.IsCancellationRequested) break;
            onStarted?.Invoke(test);
            var r = await RunAsync(test, target, ct);
            results.Add(r);
            onFinished?.Invoke(r);
        }
        return results;
    }
}
