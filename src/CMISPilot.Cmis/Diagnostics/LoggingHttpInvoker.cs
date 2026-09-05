using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using PortCMIS.Binding;
using PortCMIS.Binding.Http;

namespace CMISPilot.Cmis.Diagnostics;

/// <summary>
/// Roh-HTTP-Protokoll für das Browser Binding (T9.1, FA-80). Delegiert an den
/// PortCMIS-<see cref="DefaultHttpInvoker"/> und protokolliert Methode, URL,
/// Statuscode, Dauer und Content-Type/-Length in der Kategorie "HTTP".
/// </summary>
/// <remarks>
/// Wird von PortCMIS ausschließlich über <c>SessionParameter.HttpInvokerClass</c>
/// (Typname) + <c>Activator.CreateInstance</c> erzeugt (kein DI-Hook verfügbar),
/// deshalb parameterloser Konstruktor + Ambient-Log über
/// <see cref="DiagnosticsLogAmbient"/> (siehe dortige Doku). Der Response-Stream
/// wird bewusst NICHT gelesen, damit PortCMIS die Antwort unverändert
/// weiterverarbeiten kann – das Rohprotokoll bleibt auf Metadaten beschränkt
/// ("best effort").
/// </remarks>
public sealed class LoggingHttpInvoker : IHttpInvoker
{
    private const string Category = "HTTP";

    private readonly DefaultHttpInvoker _inner = new();
    private readonly IDiagnosticsLog _log;

    public LoggingHttpInvoker() : this(DiagnosticsLogAmbient.Current) { }

    internal LoggingHttpInvoker(IDiagnosticsLog log) => _log = log;

    public IResponse InvokeGET(UrlBuilder url, IBindingSession session) =>
        Run("GET", url, () => _inner.InvokeGET(url, session));

    public IResponse InvokeGET(UrlBuilder url, IBindingSession session, long? offset, long? length) =>
        Run("GET", url, () => _inner.InvokeGET(url, session, offset, length));

    public IResponse InvokePOST(UrlBuilder url, HttpContent content, IBindingSession session) =>
        Run("POST", url, () => _inner.InvokePOST(url, content, session));

    public IResponse InvokePUT(UrlBuilder url, IDictionary<string, string> headers, HttpContent content, IBindingSession session) =>
        Run("PUT", url, () => _inner.InvokePUT(url, headers, content, session));

    public IResponse InvokeDELETE(UrlBuilder url, IBindingSession session) =>
        Run("DELETE", url, () => _inner.InvokeDELETE(url, session));

    private IResponse Run(string method, UrlBuilder url, Func<IResponse> invoke)
    {
        var sw = Stopwatch.StartNew();
        var urlText = url?.ToString() ?? string.Empty;
        try
        {
            var response = invoke();
            sw.Stop();
            var detail = $"Status {response.StatusCode} {response.Message}".Trim();
            if (response.ContentType is not null || response.ContentLength is not null)
            {
                detail += $" ({response.ContentType}, {response.ContentLength?.ToString() ?? "?"} Bytes)";
            }

            _log.Record(DiagnosticsLogEntry.Success(Category, $"{method} {urlText}", sw.Elapsed, detail));
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.Record(DiagnosticsLogEntry.Failed(Category, $"{method} {urlText}", sw.Elapsed, ex.Message));
            throw;
        }
    }
}
