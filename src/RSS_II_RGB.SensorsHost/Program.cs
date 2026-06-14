namespace RSS_II_RGB.SensorsHost;

// Future non-AOT helper: CPU/GPU temp (LibreHardwareMonitor) + audio (WASAPI/FFT),
// streaming SensorSample JSON lines over the named pipe SensorsConstants.PipeName.
// Milestone 1 ships only this stub so the IPC seam compiles.
internal static class Program
{
    private static Task Main() => Task.CompletedTask;
}
