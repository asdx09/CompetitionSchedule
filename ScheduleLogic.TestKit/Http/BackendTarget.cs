namespace ScheduleLogic.TestKit.Http;

public sealed record BackendTarget(string Label, string BaseUrl, bool IgnoreCertificateErrors = true)
{
    public Uri BaseUri => new(BaseUrl.TrimEnd('/') + "/");

    public override string ToString() => $"{Label} ({BaseUrl})";
}
