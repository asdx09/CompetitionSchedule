namespace ScheduleLogic.TestKit.Http;

/// <summary>
/// A tesztelt backend: egy címke (pl. ".NET" vagy "PHP") és az alap URL.
/// A két backend fejlesztéskor ugyanazon a porton fut (https://localhost:7098),
/// ezért a címke mondja meg, melyik válaszolt éppen.
/// </summary>
/// <param name="Label">Megjelenített név, a riportfájl nevébe is bekerül.</param>
/// <param name="BaseUrl">A backend címe, pl. https://localhost:7098</param>
/// <param name="IgnoreCertificateErrors">
/// Fejlesztéskor a helyi (ASP.NET dev / Caddy) tanúsítványt nem minden gép
/// fogadja el; ilyenkor ezzel a tanúsítvány-ellenőrzés kikapcsolható.
/// </param>
public sealed record BackendTarget(string Label, string BaseUrl, bool IgnoreCertificateErrors = true)
{
    public Uri BaseUri => new(BaseUrl.TrimEnd('/') + "/");

    public override string ToString() => $"{Label} ({BaseUrl})";
}
