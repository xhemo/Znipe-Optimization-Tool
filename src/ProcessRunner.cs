using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Zentraler Helper zum sicheren und einheitlichen Ausführen von Prozessen, CLI-Befehlen und URLs.
    /// </summary>
    public static class ProcessRunner {
        /// <summary>
        /// Führt einen Prozess unsichtbar aus und gibt die Standardausgabe (stdout) zurück.
        /// </summary>
        public static string RunAndGetOutput(string fileName, string arguments = null, int timeoutMs = 15000) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Default
                };

                using (Process proc = Process.Start(psi)) {
                    if (proc == null) return string.Empty;
                    // Start both async reads BEFORE waiting on exit, so neither stdout nor
                    // stderr pipe can fill up and block the child. This keeps the timeout
                    // enforceable and fixes the old stdout-read/stderr-write deadlock.
                    Task<string> stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = proc.StandardError.ReadToEndAsync();
                    if (!proc.WaitForExit(timeoutMs)) {
                        try { proc.Kill(); } catch {}
                    }
                    try { Task.WaitAll(new Task[] { stdoutTask, stderrTask }); } catch {}
                    return stdoutTask.IsCompleted ? stdoutTask.Result : string.Empty;
                }
            } catch {
                return string.Empty;
            }
        }

        /// <summary>
        /// Startet ein Programm, einen Installer oder eine URL (Fire & Forget).
        /// </summary>
        public static bool Start(string fileName, string arguments = null, bool asAdmin = false) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };

                if (asAdmin) {
                    psi.Verb = "runas";
                }

                Process.Start(psi);
                return true;
            } catch {
                return false;
            }
        }
    }
}
