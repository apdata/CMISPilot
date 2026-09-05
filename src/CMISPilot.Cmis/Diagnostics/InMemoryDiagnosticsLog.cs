using System.Collections.Generic;

namespace CMISPilot.Cmis.Diagnostics;

/// <summary>
/// Standard-Implementierung von <see cref="IDiagnosticsLog"/>: begrenzter,
/// thread-sicherer Ringpuffer (T9.1). Als Singleton registriert, damit alle
/// Serveroperationen in einem gemeinsamen Protokoll landen.
/// </summary>
public sealed class InMemoryDiagnosticsLog : IDiagnosticsLog
{
    /// <summary>Default-Kapazität, falls nichts anderes angegeben wird.</summary>
    public const int DefaultCapacity = 500;

    private readonly object _lock = new();
    private readonly Queue<DiagnosticsLogEntry> _entries;

    public InMemoryDiagnosticsLog(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(capacity), "Kapazität muss größer als 0 sein.");
        }

        Capacity = capacity;
        _entries = new Queue<DiagnosticsLogEntry>(capacity);
    }

    public int Capacity { get; }

    public void Record(DiagnosticsLogEntry entry)
    {
        lock (_lock)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }
    }

    public IReadOnlyList<DiagnosticsLogEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
