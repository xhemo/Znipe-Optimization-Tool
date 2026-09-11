using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ZnipeOptimizationTool {
    public enum StressTarget {
        Both,
        CpuOnly,
        GpuOnly
    }

    public static class HardwareStressTester {
        private static readonly object _syncLock = new object();
        private static CancellationTokenSource _cts;
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private static string _liveFeedback = "Hardware im Ruhezustand. Klicke auf 'Stresstest starten', um Hardware auszulasten.";

        public static bool IsRunning { get; private set; }
        public static string LiveFeedback {
            get { return _liveFeedback; }
            private set { _liveFeedback = value; }
        }
        public static TimeSpan Elapsed { get { return _stopwatch.Elapsed; } }
        public static event EventHandler StateChanged;

        #region OpenCL Native P/Invoke
        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clGetPlatformIDs(uint num_entries, [Out] IntPtr[] platforms, out uint num_platforms);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clGetDeviceIDs(IntPtr platform, ulong device_type, uint num_entries, [Out] IntPtr[] devices, out uint num_devices);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clGetDeviceInfo(IntPtr device, uint param_name, UIntPtr param_value_size, StringBuilder param_value, out UIntPtr param_value_size_ret);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clGetDeviceInfo(IntPtr device, uint param_name, UIntPtr param_value_size, out ulong param_value, out UIntPtr param_value_size_ret);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr clCreateContext(IntPtr properties, uint num_devices, IntPtr[] devices, IntPtr pfn_notify, IntPtr user_data, out int errcode_ret);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr clCreateCommandQueue(IntPtr context, IntPtr device, ulong properties, out int errcode_ret);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr clCreateProgramWithSource(IntPtr context, uint count, string[] strings, IntPtr[] lengths, out int errcode_ret);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clBuildProgram(IntPtr program, uint num_devices, IntPtr[] device_list, string options, IntPtr pfn_notify, IntPtr user_data);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr clCreateKernel(IntPtr program, string kernel_name, out int errcode_ret);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr clCreateBuffer(IntPtr context, ulong flags, UIntPtr size, IntPtr host_ptr, out int errcode_ret);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clSetKernelArg(IntPtr kernel, uint arg_index, UIntPtr arg_size, ref IntPtr arg_value);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clSetKernelArg(IntPtr kernel, uint arg_index, UIntPtr arg_size, ref int arg_value);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clEnqueueNDRangeKernel(IntPtr command_queue, IntPtr kernel, uint work_dim, IntPtr[] global_work_offset, IntPtr[] global_work_size, IntPtr[] local_work_size, uint num_events_in_wait_list, IntPtr[] event_wait_list, IntPtr[] ev);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clFinish(IntPtr command_queue);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clReleaseKernel(IntPtr kernel);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clReleaseProgram(IntPtr program);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clReleaseCommandQueue(IntPtr command_queue);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clReleaseContext(IntPtr context);

        [DllImport("OpenCL.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int clReleaseMemObject(IntPtr memobj);
        #endregion

        public static bool Start(StressTarget target = StressTarget.Both) {
            lock (_syncLock) {
                if (IsRunning) return true;

                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                IsRunning = true;
                _stopwatch.Restart();

                bool runCpu = target == StressTarget.Both || target == StressTarget.CpuOnly;
                bool runGpu = target == StressTarget.Both || target == StressTarget.GpuOnly;

                int cpuCores = Environment.ProcessorCount;

                // 1. Maximaler CPU-Stresstest (FPU + L1/L2 Cache-Thrashing, BelowNormal-Priorität hält UI flüssig)
                if (runCpu) {
                    for (int c = 0; c < cpuCores; c++) {
                        var thread = new Thread(() => {
                            const int cacheSize = 16384; // 16K doubles = 128 KB pro Kern
                            double[] cacheBuf = new double[cacheSize];
                            for (int i = 0; i < cacheSize; i++) cacheBuf[i] = (i + 1) * 1.0001;

                            while (!token.IsCancellationRequested) {
                                for (int i = 0; i < cacheSize; i += 4) {
                                    double v0 = cacheBuf[i];
                                    double v1 = cacheBuf[i + 1];
                                    double v2 = cacheBuf[i + 2];
                                    double v3 = cacheBuf[i + 3];

                                    v0 = Math.Sqrt(Math.Abs(v0 * 1.000007 + v1 * 0.999993)) + 0.00001;
                                    v1 = (v1 * 1.000009) - (v2 * 0.999991);
                                    v2 = (v2 * 1.000011) + (v3 * 0.999989);
                                    v3 = Math.Sqrt(Math.Abs(v3 * 1.000013 - v0 * 0.999987)) + 0.00001;

                                    cacheBuf[i] = v0 > 1e8 ? 1.0001 : v0;
                                    cacheBuf[i + 1] = v1 > 1e8 ? 1.0002 : v1;
                                    cacheBuf[i + 2] = v2 > 1e8 ? 1.0003 : v2;
                                    cacheBuf[i + 3] = v3 > 1e8 ? 1.0004 : v3;
                                }
                            }
                        }) {
                            IsBackground = true,
                            Priority = ThreadPriority.Lowest
                        };
                        thread.Start();
                    }
                }

                // 2. Maximaler GPU-Stresstest (OpenCL float4 SIMD Vektor FMA + VRAM Traffic)
                if (runGpu) {
                    var gpuThread = new Thread(() => RunGpuStress(token, target, cpuCores)) {
                        IsBackground = true,
                        Priority = ThreadPriority.Lowest
                    };
                    gpuThread.Start();
                }

                if (target == StressTarget.Both)
                    LiveFeedback = string.Format("VOLLLAST AKTIV: Alle {0} CPU-Kerne & GPU voll ausgelastet.", cpuCores);
                else if (target == StressTarget.CpuOnly)
                    LiveFeedback = string.Format("VOLLLAST AKTIV: Alle {0} CPU-Kerne voll ausgelastet.", cpuCores);
                else
                    LiveFeedback = "VOLLLAST AKTIV: GPU Compute & Shader Cores werden initialisiert...";

                EventHandler handler = StateChanged;
                if (handler != null) handler(null, EventArgs.Empty);
                return true;
            }
        }

        private static void RunGpuStress(CancellationToken token, StressTarget target, int cpuCores) {
            IntPtr ctx = IntPtr.Zero, queue = IntPtr.Zero, prog = IntPtr.Zero, kernel = IntPtr.Zero, buf = IntPtr.Zero;
            try {
                uint numPlatforms;
                if (clGetPlatformIDs(0, null, out numPlatforms) != 0 || numPlatforms == 0) {
                    NotifyGpuError("OpenCL Plattform nicht gefunden.");
                    return;
                }

                IntPtr[] platforms = new IntPtr[numPlatforms];
                clGetPlatformIDs(numPlatforms, platforms, out numPlatforms);

                IntPtr targetDev = IntPtr.Zero;
                string selectedGpuName = "GPU";
                ulong bestScore = 0;

                // Alle OpenCL Plattformen und Geräte scannen -> die stärkste diskrete Grafikkarte wählen (RTX / Dedicated GPU)
                foreach (var p in platforms) {
                    uint numDevs;
                    if (clGetDeviceIDs(p, 0xFFFFFFFF, 0, null, out numDevs) == 0 && numDevs > 0) {
                        IntPtr[] devs = new IntPtr[numDevs];
                        clGetDeviceIDs(p, 0xFFFFFFFF, numDevs, devs, out numDevs);
                        foreach (var d in devs) {
                            StringBuilder sb = new StringBuilder(256);
                            UIntPtr sz;
                            clGetDeviceInfo(d, 0x102B, (UIntPtr)256, sb, out sz);
                            string name = sb.ToString();

                            ulong memBytes = 0;
                            try {
                                UIntPtr memSz;
                                clGetDeviceInfo(d, 0x101F, (UIntPtr)8, out memBytes, out memSz);
                            } catch {}

                            ulong score = memBytes / (1024UL * 1024UL); // VRAM in MB

                            // Diskrete GPUs (NVIDIA RTX / GTX / Radeon RX) massiv priorisieren gegenüber Onboard-iGPU
                            if (name.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("GTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Radeon RX", StringComparison.OrdinalIgnoreCase) >= 0) {
                                score += 50000;
                            } else if (name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) {
                                score += 25000;
                            }

                            // Onboard / Integrated Graphics abwerten
                            if (name.IndexOf("Graphics", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("UHD", StringComparison.OrdinalIgnoreCase) >= 0) {
                                if (score > 10000) score -= 10000;
                            }

                            if (score > bestScore || targetDev == IntPtr.Zero) {
                                bestScore = score;
                                targetDev = d;
                                selectedGpuName = name;
                            }
                        }
                    }
                }

                if (targetDev == IntPtr.Zero) {
                    NotifyGpuError("Kein kompatibles GPU-Gerät gefunden.");
                    return;
                }

                int err;
                ctx = clCreateContext(IntPtr.Zero, 1, new[] { targetDev }, IntPtr.Zero, IntPtr.Zero, out err);
                if (err != 0) {
                    NotifyGpuError("OpenCL Context Fehler: " + err);
                    return;
                }

                queue = clCreateCommandQueue(ctx, targetDev, 0, out err);
                if (err != 0) {
                    NotifyGpuError("OpenCL Queue Fehler: " + err);
                    return;
                }

                string src = @"
                __kernel void gpu_max_stress(__global float4* data, int numElements) {
                    int id = get_global_id(0);
                    int idx = id % numElements;
                    float4 a = data[idx];
                    float4 b = (float4)(1.0001f, 2.0002f, 3.0003f, 4.0004f);
                    float4 c = (float4)(0.5001f, 0.6002f, 0.7003f, 0.8004f);
                    for (int i = 0; i < 20000; i++) {
                        a = mad(a, b, c);
                        b = mad(b, c, a);
                        c = mad(c, a, b);
                        if (a.x > 10000.0f || a.x < -10000.0f) a = (float4)(1.0f, 1.1f, 1.2f, 1.3f);
                        if (b.y > 10000.0f || b.y < -10000.0f) b = (float4)(2.0f, 2.1f, 2.2f, 2.3f);
                    }
                    data[idx] = a + b + c;
                }";

                prog = clCreateProgramWithSource(ctx, 1, new[] { src }, null, out err);
                if (err != 0 || clBuildProgram(prog, 1, new[] { targetDev }, null, IntPtr.Zero, IntPtr.Zero) != 0) {
                    NotifyGpuError("OpenCL Shader Build Fehler.");
                    return;
                }

                kernel = clCreateKernel(prog, "gpu_max_stress", out err);
                if (err != 0) {
                    NotifyGpuError("OpenCL Kernel Fehler: " + err);
                    return;
                }

                const int numElements = 4194304; // 4M float4 = 64 MB VRAM buffer
                const int workCount = 4194304;   // 4M parallele Work Items
                buf = clCreateBuffer(ctx, 2, (UIntPtr)(numElements * 16), IntPtr.Zero, out err);
                if (err != 0) {
                    NotifyGpuError("OpenCL Buffer Fehler: " + err);
                    return;
                }

                clSetKernelArg(kernel, 0, (UIntPtr)IntPtr.Size, ref buf);
                int numElemVal = numElements;
                clSetKernelArg(kernel, 1, (UIntPtr)4, ref numElemVal);

                IntPtr[] globalWork = new IntPtr[] { (IntPtr)workCount };

                if (target == StressTarget.Both) {
                    LiveFeedback = string.Format("MAXIMAL-VOLLLAST: Alle {0} CPU-Kerne & {1} am Limit.", cpuCores, selectedGpuName);
                } else {
                    LiveFeedback = string.Format("MAXIMAL-VOLLLAST: {0} Compute & VRAM am Limit.", selectedGpuName);
                }
                EventHandler handler = StateChanged;
                if (handler != null) handler(null, EventArgs.Empty);

                // Volllast-Pipeline: Kontinuierliche Queue-Sättigung für 99-100% GPU-Power ohne UI-Hänger
                while (!token.IsCancellationRequested) {
                    for (int i = 0; i < 4; i++) {
                        clEnqueueNDRangeKernel(queue, kernel, 1, null, globalWork, null, 0, null, null);
                    }
                    clFinish(queue);
                    Thread.Yield(); // Gibt CPU-Zeitscheibe sofort an Windows/UI ab, ohne 15ms Sleep-Bremse
                }
            } catch (Exception ex) {
                NotifyGpuError("GPU Stresstest abgebrochen: " + ex.Message);
            } finally {
                if (buf != IntPtr.Zero) try { clReleaseMemObject(buf); } catch {}
                if (kernel != IntPtr.Zero) try { clReleaseKernel(kernel); } catch {}
                if (prog != IntPtr.Zero) try { clReleaseProgram(prog); } catch {}
                if (queue != IntPtr.Zero) try { clReleaseCommandQueue(queue); } catch {}
                if (ctx != IntPtr.Zero) try { clReleaseContext(ctx); } catch {}
            }
        }

        private static void NotifyGpuError(string msg) {
            LiveFeedback = msg;
            EventHandler handler = StateChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public static void Stop() {
            lock (_syncLock) {
                if (!IsRunning) return;
                try {
                    if (_cts != null) {
                        _cts.Cancel();
                        _cts.Dispose();
                    }
                } catch {}
                _cts = null;
                _stopwatch.Stop();
                IsRunning = false;
                LiveFeedback = string.Format("Stresstest beendet. Gesamtdauer: {0:mm\\:ss}. Hardware kühlt wieder ab.", Elapsed);
                EventHandler handler = StateChanged;
                if (handler != null) handler(null, EventArgs.Empty);
            }
        }
    }
}
