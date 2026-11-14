using System.Runtime.InteropServices;

namespace Engine.Desktop;

internal static unsafe class MacAudioOutput
{
    // AudioObjectPropertyAddress for the main element in the global scope.
    [StructLayout(LayoutKind.Sequential)]
    private struct Property(uint selector)
    {
        public uint Selector = selector;
        public uint Scope = 0x676c6f62; // 'glob'
        public uint Element = 0;
    }

    [DllImport("/System/Library/Frameworks/CoreAudio.framework/CoreAudio")]
    private static extern int AudioObjectGetPropertyData(uint id, Property* property, uint qualifierSize,
        void* qualifier, uint* size, void* value);

    private static bool Read<T>(uint id, uint selector, out T value) where T : unmanaged
    {
        var property = new Property(selector);
        uint size = (uint)sizeof(T);
        T result = default;
        bool ok = AudioObjectGetPropertyData(id, &property, 0, null, &size, &result) == 0 && size == sizeof(T);
        value = result;
        return ok;
    }

    internal static double DeviceBufferSeconds()
    {
        return Read(1, 0x644f7574, out uint device) &&         // kAudioHardwarePropertyDefaultOutputDevice
            Read(device, 0x6673697a, out uint frames) &&      // kAudioDevicePropertyBufferFrameSize
            Read(device, 0x6e737274, out double rate) &&      // kAudioDevicePropertyNominalSampleRate
            rate > 0 ? frames / rate : 0;
    }
}
