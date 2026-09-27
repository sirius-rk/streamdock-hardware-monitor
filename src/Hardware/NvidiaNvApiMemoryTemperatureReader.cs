using System.Runtime.InteropServices;

namespace StreamDockHardwareMonitor.Hardware;

public sealed class NvidiaNvApiMemoryTemperatureReader : IDisposable
{
    private const uint InitializeInterfaceId = 0x0150E828;
    private const uint UnloadInterfaceId = 0xD22BDD7E;
    private const uint EnumeratePhysicalGpusInterfaceId = 0xE5AC921F;
    private const uint GetThermalSettingsInterfaceId = 0xE3640A56;
    private const int MaximumPhysicalGpus = 64;
    private const int MaximumThermalSensors = 3;
    private const int ThermalSettingsV2Size = 68;
    private const int NvApiSuccess = 0;
    private const int MemoryThermalTarget = 2;
    private const uint AllThermalSensors = 15;

    private IntPtr _library;
    private InitializeDelegate? _initialize;
    private UnloadDelegate? _unload;
    private EnumeratePhysicalGpusDelegate? _enumeratePhysicalGpus;
    private GetThermalSettingsDelegate? _getThermalSettings;
    private bool _initializationAttempted;
    private bool _initialized;

    public double? ReadMemoryTemperature()
    {
        if (!EnsureInitialized())
        {
            return null;
        }

        var handles = new IntPtr[MaximumPhysicalGpus];
        uint count = 0;
        if (_enumeratePhysicalGpus!(handles, ref count) != NvApiSuccess)
        {
            return null;
        }

        var handleCount = Math.Min((int)count, handles.Length);
        for (var index = 0; index < handleCount; index++)
        {
            var temperature = ReadMemoryTemperature(handles[index]);
            if (temperature is not null)
            {
                return temperature;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_library == IntPtr.Zero)
        {
            return;
        }

        if (_initialized)
        {
            try
            {
                _ = _unload?.Invoke();
            }
            catch
            {
                // The driver may keep NVAPI loaded until this plugin process exits.
            }
        }

        NativeLibrary.Free(_library);
        _library = IntPtr.Zero;
        _initialized = false;
    }

    private double? ReadMemoryTemperature(IntPtr gpuHandle)
    {
        var buffer = Marshal.AllocHGlobal(ThermalSettingsV2Size);
        try
        {
            for (var offset = 0; offset < ThermalSettingsV2Size; offset += sizeof(int))
            {
                Marshal.WriteInt32(buffer, offset, 0);
            }

            var version = (uint)ThermalSettingsV2Size | (2u << 16);
            Marshal.WriteInt32(buffer, unchecked((int)version));

            var thermalStatus = _getThermalSettings!(gpuHandle, AllThermalSensors, buffer);
            if (thermalStatus != NvApiSuccess)
            {
                return null;
            }

            var sensorCount = Math.Clamp(Marshal.ReadInt32(buffer, sizeof(int)), 0, MaximumThermalSensors);
            for (var index = 0; index < sensorCount; index++)
            {
                var sensorOffset = sizeof(int) * 2 + index * (sizeof(int) * 5);
                var currentTemperature = Marshal.ReadInt32(buffer, sensorOffset + sizeof(int) * 3);
                var target = Marshal.ReadInt32(buffer, sensorOffset + sizeof(int) * 4);
                if (target == MemoryThermalTarget && currentTemperature is >= 0 and <= 150)
                {
                    return currentTemperature;
                }
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private bool EnsureInitialized()
    {
        if (_initializationAttempted)
        {
            return _initialized;
        }

        _initializationAttempted = true;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var libraryPath = Path.Combine(systemDirectory, "nvapi64.dll");
            if (!File.Exists(libraryPath) || !NativeLibrary.TryLoad(libraryPath, out _library))
            {
                return false;
            }

            var queryInterfacePointer = NativeLibrary.GetExport(_library, "nvapi_QueryInterface");
            var queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryInterfacePointer);

            _initialize = GetDelegate<InitializeDelegate>(queryInterface, InitializeInterfaceId);
            _unload = GetDelegate<UnloadDelegate>(queryInterface, UnloadInterfaceId);
            _enumeratePhysicalGpus = GetDelegate<EnumeratePhysicalGpusDelegate>(
                queryInterface,
                EnumeratePhysicalGpusInterfaceId);
            _getThermalSettings = GetDelegate<GetThermalSettingsDelegate>(
                queryInterface,
                GetThermalSettingsInterfaceId);

            _initialized = _initialize is not null
                && _enumeratePhysicalGpus is not null
                && _getThermalSettings is not null
                && _initialize() == NvApiSuccess;
            if (!_initialized)
            {
                ReleaseLibrary();
            }

            return _initialized;
        }
        catch (Exception exception) when (
            exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or MarshalDirectiveException
                or ExternalException)
        {
            ReleaseLibrary();
            return false;
        }
    }

    private static TDelegate? GetDelegate<TDelegate>(QueryInterfaceDelegate queryInterface, uint id)
        where TDelegate : Delegate
    {
        var functionPointer = queryInterface(id);
        return functionPointer == IntPtr.Zero
            ? null
            : Marshal.GetDelegateForFunctionPointer<TDelegate>(functionPointer);
    }

    private void ReleaseLibrary()
    {
        if (_library != IntPtr.Zero)
        {
            NativeLibrary.Free(_library);
            _library = IntPtr.Zero;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterfaceDelegate(uint interfaceId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int UnloadDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumeratePhysicalGpusDelegate(
        [Out, MarshalAs(UnmanagedType.LPArray, SizeConst = MaximumPhysicalGpus)] IntPtr[] handles,
        ref uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetThermalSettingsDelegate(IntPtr gpuHandle, uint sensorIndex, IntPtr settings);
}
