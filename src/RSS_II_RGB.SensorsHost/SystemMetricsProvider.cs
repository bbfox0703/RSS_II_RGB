using System.Runtime.InteropServices;
using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.SensorsHost;

/// <summary>
/// CPU / memory / GPU utilisation and GPU temperature, all without admin:
/// CPU% via GetSystemTimes, memory via GlobalMemoryStatusEx, GPU% + temperature
/// via NVML (NVIDIA). Values stay NaN (and aren't emitted) when unavailable.
/// </summary>
internal sealed class SystemMetricsProvider : ISensorProvider
{
    private readonly object _gate = new();
    private double _cpu = double.NaN;
    private double _mem = double.NaN;
    private double _gpuUtil = double.NaN;
    private double _gpuTemp = double.NaN;

    private ulong _prevIdle;
    private ulong _prevTotal;
    private bool _haveCpuBaseline;

    private nint _nvmlDevice;
    private bool _nvmlReady;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public void Start()
    {
        TryInitNvml();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public IEnumerable<SensorSample> Poll()
    {
        long now = Environment.TickCount64;
        double cpu, mem, gpuUtil, gpuTemp;
        lock (_gate)
        {
            cpu = _cpu;
            mem = _mem;
            gpuUtil = _gpuUtil;
            gpuTemp = _gpuTemp;
        }

        if (!double.IsNaN(cpu)) yield return new SensorSample(SensorKind.CpuUtil, new[] { cpu }, now);
        if (!double.IsNaN(mem)) yield return new SensorSample(SensorKind.MemUtil, new[] { mem }, now);
        if (!double.IsNaN(gpuUtil)) yield return new SensorSample(SensorKind.GpuUtil, new[] { gpuUtil }, now);
        if (!double.IsNaN(gpuTemp)) yield return new SensorSample(SensorKind.GpuTemp, new[] { gpuTemp }, now);
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            double cpu = ReadCpu();
            double mem = ReadMem();
            (double gpuUtil, double gpuTemp) = ReadGpu();
            lock (_gate)
            {
                if (!double.IsNaN(cpu)) _cpu = cpu;
                _mem = mem;
                _gpuUtil = gpuUtil;
                _gpuTemp = gpuTemp;
            }

            try
            {
                await Task.Delay(1000, ct);
            }
            catch
            {
                break;
            }
        }
    }

    private double ReadCpu()
    {
        if (!GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user))
        {
            return double.NaN;
        }

        ulong idleT = ToUlong(idle);
        ulong total = ToUlong(kernel) + ToUlong(user); // kernel time includes idle
        if (!_haveCpuBaseline)
        {
            _prevIdle = idleT;
            _prevTotal = total;
            _haveCpuBaseline = true;
            return double.NaN; // need a delta
        }

        ulong dIdle = idleT - _prevIdle;
        ulong dTotal = total - _prevTotal;
        _prevIdle = idleT;
        _prevTotal = total;
        if (dTotal == 0)
        {
            return double.NaN;
        }
        return Math.Clamp((double)(dTotal - dIdle) / dTotal * 100.0, 0, 100);
    }

    private static double ReadMem()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? status.dwMemoryLoad : double.NaN;
    }

    private (double Util, double Temp) ReadGpu()
    {
        if (!_nvmlReady)
        {
            return (double.NaN, double.NaN);
        }

        double util = double.NaN;
        double temp = double.NaN;
        try
        {
            if (nvmlDeviceGetUtilizationRates(_nvmlDevice, out NvmlUtilization rates) == 0)
            {
                util = rates.Gpu;
            }
            if (nvmlDeviceGetTemperature(_nvmlDevice, 0, out uint t) == 0)
            {
                temp = t;
            }
        }
        catch
        {
            // driver/NVML hiccup — report no data this tick
        }
        return (util, temp);
    }

    private void TryInitNvml()
    {
        try
        {
            if (nvmlInit_v2() != 0)
            {
                return;
            }
            if (nvmlDeviceGetHandleByIndex_v2(0, out nint device) != 0)
            {
                return;
            }
            _nvmlDevice = device;
            _nvmlReady = true;
        }
        catch
        {
            _nvmlReady = false; // no NVIDIA GPU / nvml.dll
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(1500); } catch { }
        if (_nvmlReady)
        {
            try { nvmlShutdown(); } catch { }
        }
        _cts?.Dispose();
    }

    private static ulong ToUlong(FILETIME ft) => ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlUtilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    private static extern int nvmlInit_v2();

    [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
    private static extern int nvmlShutdown();

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out nint device);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetUtilizationRates")]
    private static extern int nvmlDeviceGetUtilizationRates(nint device, out NvmlUtilization util);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
    private static extern int nvmlDeviceGetTemperature(nint device, int sensorType, out uint temp);
}
