using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using RcCar.Core.Abstractions;
using RcCar.Core.Models;

namespace RcCar.App.Infrastructure;

/// <summary>Durable detailed events plus a separate human-readable session summary.</summary>
public sealed class SessionLog : ISessionEvents, IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter detailed;
    private readonly StreamWriter summary;
    private readonly ConcurrentQueue<SessionEvent> pending = new();
    private bool disposed;
    private bool fileLoggingFailed;

    public string Folder { get; }

    public SessionLog(string? baseFolder = null)
    {
        baseFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RcCar", "sessions");
        Folder = Path.Combine(baseFolder, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}");
        Directory.CreateDirectory(Folder);
        detailed = new StreamWriter(Path.Combine(Folder, "events.jsonl"))
        {
            AutoFlush = true
        };
        summary = new StreamWriter(Path.Combine(Folder, "summary.md"))
        {
            AutoFlush = true
        };
        summary.WriteLine("# RC car session\n\nTransmission completion is not proof of physical motion. User observations are recorded separately.\n");
    }

    public void Write(SessionEventKind kind, string message)
    {
        var entry = new SessionEvent(DateTimeOffset.Now, kind, message);
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            // A full/unwritable disk must not throw out of a native radio callback.
            if (!fileLoggingFailed)
            {
                try
                {
                    detailed.WriteLine(JsonSerializer.Serialize(entry));
                    if (kind != SessionEventKind.Transmission && !message.StartsWith("Publisher:"))
                    {
                        summary.WriteLine($"- {entry.Timestamp:O} [{kind}] {message.ReplaceLineEndings(" ")}");
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    fileLoggingFailed = true;
                    pending.Enqueue(new SessionEvent(DateTimeOffset.Now, SessionEventKind.Error, "Session file logging failed; events remain visible in the window. " + exception.Message));
                }
            }

            pending.Enqueue(entry);
        }
    }

    public bool TryRead(out SessionEvent? entry) => pending.TryDequeue(out entry);
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                detailed.Dispose();
            }
            catch (IOException) when (fileLoggingFailed)
            {
            }

            try
            {
                summary.Dispose();
            }
            catch (IOException) when (fileLoggingFailed)
            {
            }
        }
    }
}
