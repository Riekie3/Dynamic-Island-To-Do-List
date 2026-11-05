using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MacSpike;

/// <summary>Calls into macOS through the Objective-C runtime and Carbon. Every public method is a no-op off macOS.</summary>
internal static unsafe class Mac
{
    private const string Objc = "/usr/lib/libobjc.A.dylib";
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    [StructLayout(LayoutKind.Sequential)] public struct CGPoint { public double X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct CGRect { public double X, Y, W, H; }
    [StructLayout(LayoutKind.Sequential)] public struct Insets { public double Top, Left, Bottom, Right; }

    [DllImport(Objc)] private static extern IntPtr objc_getClass(string name);
    [DllImport(Objc)] private static extern IntPtr sel_registerName(string name);
    [DllImport(Objc)] private static extern IntPtr object_getClass(IntPtr obj);
    [DllImport(Objc)] private static extern IntPtr class_getName(IntPtr cls);

    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr o, IntPtr s);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr o, IntPtr s, IntPtr a);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr o, IntPtr s, long a);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr o, IntPtr s, ulong a);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr o, IntPtr s, CGPoint a);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr o, IntPtr s, byte a);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern IntPtr SendOut(IntPtr o, IntPtr s, out IntPtr error);

    // Large structs come back differently: registers on Apple Silicon, a hidden pointer on Intel.
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern CGRect SendRect(IntPtr o, IntPtr s);
    [DllImport(Objc, EntryPoint = "objc_msgSend_stret")] private static extern void SendRectStret(out CGRect r, IntPtr o, IntPtr s);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] private static extern Insets SendInsets(IntPtr o, IntPtr s);
    [DllImport(Objc, EntryPoint = "objc_msgSend_stret")] private static extern void SendInsetsStret(out Insets r, IntPtr o, IntPtr s);

    private static bool IsIntel => RuntimeInformation.ProcessArchitecture == Architecture.X64;
    public static bool Active => OperatingSystem.IsMacOS();

    private static IntPtr C(string n) => objc_getClass(n);
    private static IntPtr S(string n) => sel_registerName(n);
    private static IntPtr NSApp => Send(C("NSApplication"), S("sharedApplication"));

    private static CGRect Rect(IntPtr o, string sel)
    {
        if (!IsIntel) return SendRect(o, S(sel));
        SendRectStret(out var r, o, S(sel));
        return r;
    }

    private static string Str(IntPtr nsString) =>
        nsString == IntPtr.Zero ? "(nil)" : Marshal.PtrToStringUTF8(Send(nsString, S("UTF8String"))) ?? "";

    // ---- window ----

    private const ulong JoinAllSpaces = 1, Stationary = 16, IgnoresCycle = 64, FullScreenAuxiliary = 256;
    private const long StatusWindowLevel = 25;

    /// <summary>Avalonia hands out either the NSView or the NSWindow, depending on version.</summary>
    public static IntPtr WindowOf(IntPtr handle, string? descriptor) =>
        !Active || handle == IntPtr.Zero ? IntPtr.Zero
        : descriptor == "NSView" ? Send(handle, S("window")) : handle;

    public static void SetAccessoryApp()
    {
        if (Active) Log.Write($"setActivationPolicy accessory: {Send(NSApp, S("setActivationPolicy:"), 1L)}");
    }

    public static void StyleIsland(IntPtr w, bool overFullScreen)
    {
        if (w == IntPtr.Zero) return;
        Send(w, S("setLevel:"), StatusWindowLevel);
        Send(w, S("setCollectionBehavior:"), JoinAllSpaces | Stationary | IgnoresCycle | (overFullScreen ? FullScreenAuxiliary : 0));
        Send(w, S("setHasShadow:"), (byte)0);
        Send(w, S("setHidesOnDeactivate:"), (byte)0);
        Log.Write($"window class={ClassName(w)} level={(long)Send(w, S("level"))} behavior={(ulong)Send(w, S("collectionBehavior"))} " +
                  $"canBecomeKey={(byte)Send(w, S("canBecomeKeyWindow")) != 0}");
    }

    public static string ClassName(IntPtr o) => Marshal.PtrToStringUTF8(class_getName(object_getClass(o))) ?? "?";

    public static int ScreenCount => Active ? (int)Send(Send(C("NSScreen"), S("screens")), S("count")) : 1;

    private static IntPtr Screen(int i) => Send(Send(C("NSScreen"), S("screens")), S("objectAtIndex:"), (ulong)i);

    /// <summary>Top centre of screen i, directly below the menu bar and the notch. Cocoa's origin is the bottom-left.</summary>
    public static void PlaceBelowMenuBar(IntPtr w, int screen, double widthPt, double heightPt)
    {
        if (w == IntPtr.Zero) return;
        var s = Screen(screen % ScreenCount);
        var frame = Rect(s, "frame");
        var visible = Rect(s, "visibleFrame");
        var origin = new CGPoint { X = frame.X + (frame.W - widthPt) / 2, Y = visible.Y + visible.H - heightPt };
        Send(w, S("setFrameOrigin:"), origin);
    }

    /// <summary>Where the window really is compared with where it should be: top edge at the menu bar, centred, right size.</summary>
    public static (bool Ok, string Text) CheckFrame(IntPtr w, int screen, double widthPt, double heightPt)
    {
        if (w == IntPtr.Zero) return (true, "no native window");
        var s = Screen(screen % ScreenCount);
        var f = Rect(w, "frame");
        var sf = Rect(s, "frame");
        var v = Rect(s, "visibleFrame");
        var topGap = f.Y + f.H - (v.Y + v.H);
        var centreGap = f.X + f.W / 2 - (sf.X + sf.W / 2);
        var ok = Math.Abs(topGap) < 1 && Math.Abs(centreGap) < 1 && Math.Abs(f.W - widthPt) < 1 && Math.Abs(f.H - heightPt) < 1;
        return (ok, $"size {f.W}x{f.H} (wanted {widthPt}x{heightPt}), top edge {topGap:+0;-0;0} pt from the menu bar line, centre {centreGap:+0;-0;0} pt");
    }

    public static void LogScreens()
    {
        if (!Active) return;
        for (int i = 0; i < ScreenCount; i++)
        {
            var s = Screen(i);
            var f = Rect(s, "frame");
            var v = Rect(s, "visibleFrame");
            Insets a;
            if (IsIntel) SendInsetsStret(out a, s, S("safeAreaInsets")); else a = SendInsets(s, S("safeAreaInsets"));
            Log.Write($"screen {i}: frame {f.W}x{f.H} at {f.X},{f.Y}; visible {v.W}x{v.H} at {v.X},{v.Y}; " +
                      $"safeArea top {a.Top} (above 0 means a notch)");
        }
    }

    // ---- focus ----

    private static IntPtr _previous;

    public static string FrontmostApp()
    {
        if (!Active) return "(not a Mac)";
        var app = Send(Send(C("NSWorkspace"), S("sharedWorkspace")), S("frontmostApplication"));
        return app == IntPtr.Zero ? "(none)" : Str(Send(app, S("localizedName")));
    }

    /// <summary>The panel needs typing, so it has to take focus. Remembers who had it, to hand it back on collapse.</summary>
    public static void TakeFocus(IntPtr w)
    {
        if (w == IntPtr.Zero) return;
        var app = Send(Send(C("NSWorkspace"), S("sharedWorkspace")), S("frontmostApplication"));
        if (app != IntPtr.Zero && app != Send(C("NSRunningApplication"), S("currentApplication")))
            _previous = Send(app, S("retain"));
        Send(NSApp, S("activateIgnoringOtherApps:"), (byte)1);
        Send(w, S("makeKeyAndOrderFront:"), IntPtr.Zero);
    }

    public static void GiveFocusBack()
    {
        if (!Active || _previous == IntPtr.Zero) return;
        Send(_previous, S("activateWithOptions:"), 0UL);
        Send(_previous, S("release"));
        _previous = IntPtr.Zero;
    }

    public static bool IsKey(IntPtr w) => w != IntPtr.Zero && (byte)Send(w, S("isKeyWindow")) != 0;

    // ---- global hotkey: Carbon, needs no Accessibility permission ----

    [StructLayout(LayoutKind.Sequential)] private struct EventTypeSpec { public uint Class, Kind; }
    [StructLayout(LayoutKind.Sequential)] private struct EventHotKeyID { public uint Signature, Id; }

    [DllImport(Carbon)] private static extern IntPtr GetApplicationEventTarget();
    [DllImport(Carbon)] private static extern int InstallEventHandler(IntPtr target, IntPtr handler, uint count, ref EventTypeSpec types, IntPtr user, out IntPtr handlerRef);
    [DllImport(Carbon)] private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyID id, IntPtr target, uint options, out IntPtr hotKeyRef);

    private const uint KeyT = 17, ControlKey = 0x1000, OptionKey = 0x0800;
    private static Action? _onHotkey;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnHotkey(IntPtr next, IntPtr evt, IntPtr user)
    {
        _onHotkey?.Invoke();
        return 0;
    }

    public static void RegisterHotkey(Action pressed)
    {
        if (!Active) return;
        _onHotkey = pressed;
        var spec = new EventTypeSpec { Class = 0x6B657962 /* 'keyb' */, Kind = 5 /* hot key pressed */ };
        delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> h = &OnHotkey;
        int a = InstallEventHandler(GetApplicationEventTarget(), (IntPtr)h, 1, ref spec, IntPtr.Zero, out _);
        int b = RegisterEventHotKey(KeyT, ControlKey | OptionKey, new EventHotKeyID { Signature = 0x54534B49, Id = 1 },
            GetApplicationEventTarget(), 0, out _);
        Log.Write($"hotkey Control+Option+T: handler result {a}, register result {b} (0 means OK)");
    }

    // ---- start at login ----

    private static IntPtr MainAppService()
    {
        NativeLibrary.Load("/System/Library/Frameworks/ServiceManagement.framework/ServiceManagement");
        return Send(C("SMAppService"), S("mainAppService"));
    }

    private static string StatusName(long s) => s switch
    {
        0 => "notRegistered", 1 => "enabled", 2 => "requiresApproval", 3 => "notFound", _ => $"unknown({s})",
    };

    public static string LoginItemStatus() => Active ? StatusName((long)Send(MainAppService(), S("status"))) : "(not a Mac)";

    public static void LoginItem(bool on)
    {
        if (!Active) return;
        var svc = MainAppService();
        var ok = (byte)SendOut(svc, S(on ? "registerAndReturnError:" : "unregisterAndReturnError:"), out var err) != 0;
        var msg = err == IntPtr.Zero ? "" : " error: " + Str(Send(err, S("localizedDescription")));
        Log.Write($"SMAppService {(on ? "register" : "unregister")}: ok={ok}{msg}; status now {LoginItemStatus()}");
    }

    private static string AgentPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", "com.riesources.taskisland.spike.plist");

    public static void LaunchAgent(bool on)
    {
        if (!Active) return;
        if (!on)
        {
            File.Delete(AgentPath);
            Log.Write("LaunchAgent removed");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(AgentPath)!);
        File.WriteAllText(AgentPath, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0"><dict>
              <key>Label</key><string>com.riesources.taskisland.spike</string>
              <key>ProgramArguments</key><array><string>{Environment.ProcessPath}</string></array>
              <key>RunAtLoad</key><true/>
            </dict></plist>
            """);
        Log.Write($"LaunchAgent written: {AgentPath}");
    }
}
