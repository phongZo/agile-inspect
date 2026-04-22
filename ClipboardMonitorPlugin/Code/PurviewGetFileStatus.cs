#nullable enable
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;

namespace ClipboardMonitorPlugin.Code
{
    /// <summary>
    /// Queries Microsoft Purview Information Protection file status via PowerShell
    /// <c>Get-FileStatus</c> (module <c>MicrosoftPurviewInformationProtection</c>).
    /// </summary>
    internal static class PurviewGetFileStatus
    {
        private const int PowerShellTimeoutMs = 4000;
        private static readonly object _gate = new();
        private static PersistentPowerShell? _ps;

        internal readonly record struct Result(bool? IsLabeled, bool? IsRMSProtected, string MainLabelName, bool Ok, string Error);

        internal static void Initialize()
        {
            lock (_gate)
            {
                _ps ??= new PersistentPowerShell();
            }
        }

        internal static void Shutdown()
        {
            lock (_gate)
            {
                _ps?.Dispose();
                _ps = null;
            }
        }

        internal static Result Query(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return new Result(null, null, "", false, "empty path");

            Initialize();

            PersistentPowerShell ps;
            lock (_gate)
            {
                ps = _ps!;
            }

            // Output: "<IsLabeled>|<IsRMSProtected>|<MainLabelName>"
            // Keep it one line and avoid CLIXML by not using stderr.
            var psPath = filePath.Replace("'", "''");
            var script =
                "$ErrorActionPreference='Stop'; " +
                "try { " +
                "  $s = Get-FileStatus -Path '" + psPath + "'; " +
                "  $label = ($s.MainLabelName -as [string]); " +
                "  Write-Output (($s.IsLabeled.ToString()) + '|' + ($s.IsRMSProtected.ToString()) + '|' + $label) " +
                "} catch { " +
                "  Write-Output ('__ERROR__|' + ($_.Exception.Message -as [string])) " +
                "}";

            var line = ps.InvokeOneLine(script, PowerShellTimeoutMs);
            if (line == null)
                return new Result(null, null, "", false, "powershell timeout");

            line = line.Trim();
            if (line.StartsWith("__ERROR__|", StringComparison.OrdinalIgnoreCase))
                return new Result(null, null, "", false, line.Substring("__ERROR__|".Length));

            var parts = line.Split('|');
            if (parts.Length < 3)
                return new Result(null, null, "", false, "unexpected output: " + line);

            bool? isLabeled = TryParseBool(parts[0]);
            bool? isRms = TryParseBool(parts[1]);
            var label = parts[2];
            if (string.Equals(label, "null", StringComparison.OrdinalIgnoreCase))
                label = "";

            return new Result(isLabeled, isRms, string.IsNullOrWhiteSpace(label) ? "" : label, true, "");
        }

        private static bool? TryParseBool(string s)
        {
            if (bool.TryParse(s?.Trim(), out var b)) return b;
            return null;
        }

        private sealed class PersistentPowerShell : IDisposable
        {
            private readonly Process _process;
            private readonly ConcurrentDictionary<string, TaskCompletionSource<string?>> _pending = new(StringComparer.Ordinal);
            private int _nextId;

            internal PersistentPowerShell()
            {
                _process = new Process();
                _process.StartInfo.FileName = "powershell.exe";
                // -Command - reads commands from stdin until EOF.
                // -OutputFormat Text reduces CLIXML surprises.
                _process.StartInfo.Arguments = "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -OutputFormat Text -ExecutionPolicy Bypass -Command -";
                _process.StartInfo.RedirectStandardInput = true;
                _process.StartInfo.RedirectStandardOutput = true;
                _process.StartInfo.RedirectStandardError = false;
                _process.StartInfo.UseShellExecute = false;
                _process.StartInfo.CreateNoWindow = true;
                _process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                _process.Start();
                _ = Task.Run(ReadLoop);
            }

            internal string? InvokeOneLine(string script, int timeoutMs)
            {
                if (_process.HasExited) return null;

                var id = Interlocked.Increment(ref _nextId).ToString();
                var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending[id] = tcs;

                // Single-line envelope with BEGIN/END markers, with the payload printed in between.
                // Ensure output is line-based (Write-Output).
                var cmd =
                    "Write-Output \"__BEGIN__" + id + "\"; " +
                    script + "; " +
                    "Write-Output \"__END__" + id + "\"";

                try
                {
                    _process.StandardInput.WriteLine(cmd);
                    _process.StandardInput.Flush();
                }
                catch
                {
                    _pending.TryRemove(id, out _);
                    return null;
                }

                if (!tcs.Task.Wait(timeoutMs))
                {
                    _pending.TryRemove(id, out _);
                    return null;
                }

                return tcs.Task.Result;
            }

            private async Task ReadLoop()
            {
                try
                {
                    while (!_process.HasExited)
                    {
                        var line = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;

                        // We expect: BEGIN, then one payload line, then END.
                        if (!line.StartsWith("__BEGIN__", StringComparison.Ordinal))
                            continue;

                        var id = line.Substring("__BEGIN__".Length);
                        var payload = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                        var end = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);

                        if (payload == null || end == null)
                            break;

                        if (!end.Equals("__END__" + id, StringComparison.Ordinal))
                            continue;

                        if (_pending.TryRemove(id, out var tcs))
                            tcs.TrySetResult(payload);
                    }
                }
                catch
                {
                    // Best-effort; pending callers will time out.
                }
            }

            public void Dispose()
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        try
                        {
                            _process.StandardInput.WriteLine("exit");
                            _process.StandardInput.Flush();
                        }
                        catch { /* ignore */ }

                        if (!_process.WaitForExit(1500))
                        {
                            try { _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                        }
                    }
                }
                catch { /* ignore */ }

                try { _process.Dispose(); } catch { /* ignore */ }
            }
        }
    }
}
