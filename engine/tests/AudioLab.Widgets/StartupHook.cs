using Brutal.SdlApi;

// Loaded only by the Release player requested by verify_widgets.py. The game polls this
// device through its normal SDL gamepad path, including discovery, release and dead zones.
internal static class StartupHook
{
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("MONOON_TEST_PAD") is not {} directory) return;
        new Thread(() => {
            try { Drive(directory); }
            catch (Exception e) { File.WriteAllText(Path.Combine(directory, "pad-error"), e.ToString()); }
        }) { IsBackground = true }.Start();
    }

    private static unsafe void Drive(string directory)
    {
        string Ticket(string name) => Path.Combine(directory, name);
        while (!File.Exists(Ticket("attach"))) Thread.Sleep(80);
        var desc = new SdlVirtualJoystickDesc { version = (uint)sizeof(SdlVirtualJoystickDesc),
            type = (ushort)SdlJoystickType.Gamepad, naxes = 6, nbuttons = 15, button_mask = 0x7fff, axis_mask = 63 };
        var id = Sdl.Joystick.AttachVirtualJoystick(&desc);
        if (id == 0 || Sdl.Gamepad.GetGamepads().FirstOrDefault() != id)
            throw new InvalidOperationException("The test needs the SDL virtual controller as the first gamepad.");
        try
        {
            using var joystick = Sdl.Joystick.OpenJoystick(id);
            Thread.Sleep(1600);
            File.WriteAllText(Ticket("attached"), id.ToString());
            bool Requested(int index, out SdlGamepadButton button)
            {
                foreach (var candidate in new[] { SdlGamepadButton.Start, SdlGamepadButton.DpadLeft })
                    if (File.Exists(Ticket($"press-{index}-{candidate}"))) { button = candidate; return true; }
                button = default;
                return false;
            }
            for (int i = 0; ; i++)
            {
                SdlGamepadButton button;
                while (!Requested(i, out button))
                {
                    if (File.Exists(Ticket("detach"))) return;
                    Thread.Sleep(50);
                }
                foreach (bool down in new[] { true, false })
                {
                    if (!Sdl.Joystick.SetJoystickVirtualButton(joystick, (int)button, down))
                        throw new InvalidOperationException(Sdl.PInvoke.GetError());
                    Thread.Sleep(650);
                }
                File.WriteAllText(Ticket($"released-{i}"), button.ToString());
            }
        }
        finally { Sdl.Joystick.DetachVirtualJoystick(id); File.WriteAllText(Ticket("detached"), ""); }
    }
}
