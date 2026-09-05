using System;
using System.IO;

namespace CMISPilot.Cmis.Models;

/// <summary>
/// Content-Stream eines Dokuments (FA-40). Der Aufrufer ist für das Schließen
/// von <see cref="Stream"/> verantwortlich (via using/Dispose).
/// </summary>
public sealed class CmisContentDto : IDisposable
{
    public required Stream Stream { get; init; }
    public string? FileName { get; init; }
    public string? MimeType { get; init; }
    public long? Length { get; init; }

    public void Dispose() => Stream?.Dispose();
}
