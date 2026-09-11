using System;
using System.Net;
using System.Net.NetworkInformation;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Schlanker ICMP-Echo-Sender basierend auf System.Net.NetworkInformation.Ping (IcmpSendEcho2).
    /// </summary>
    public sealed class PrecisePing : IDisposable {
        private readonly Ping _ping;

        private PrecisePing() {
            _ping = new Ping();
        }

        public static PrecisePing Create() {
            return new PrecisePing();
        }

        public bool TryPing(IPAddress target, int timeoutMs, out double rttMs) {
            rttMs = double.NaN;
            if (target == null || _ping == null) return false;
            try {
                var reply = _ping.Send(target, timeoutMs);
                if (reply != null && reply.Status == IPStatus.Success) {
                    rttMs = reply.RoundtripTime;
                    return true;
                }
                return false;
            } catch {
                return false;
            }
        }

        public void Dispose() {
            try { if (_ping != null) _ping.Dispose(); } catch { }
        }
    }
}
