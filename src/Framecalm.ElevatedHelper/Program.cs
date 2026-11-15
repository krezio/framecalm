using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// Framecalm's only elevated part. It does exactly three things and refuses anything else:
//   capture --process <name> --seconds <5-120> --out <file under %LOCALAPPDATA%\Framecalm\captures>
//   restore-point --description <text>
//   tweak --in <request under %LOCALAPPDATA%\Framecalm\requests>: writes registry values and power settings,
//         but only keys listed in the built-in tweak catalog (Data/tweaks.json), and only values of the right type.
// The main app never runs as admin.

static string? Arg(string[] a, string n) { var i = Array.IndexOf(a, n); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }

var captures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Framecalm", "captures");

try
{
    switch (args.FirstOrDefault())
    {
        case "capture":
        {
            var proc = Arg(args, "--process") ?? "";
            var secs = int.TryParse(Arg(args, "--seconds"), out var s) ? s : 0;
            var outFile = Path.GetFullPath(Arg(args, "--out") ?? "");
            if (!Regex.IsMatch(proc, @"^[A-Za-z0-9 ._\-]{1,80}$")) return Fail("bad process name");
            if (secs is < 5 or > 120) return Fail("seconds must be 5-120");
            if (!outFile.StartsWith(Path.GetFullPath(captures) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !outFile.EndsWith(".csv")) return Fail("output must be in the captures folder");
            var pm = new[] { Path.Combine(AppContext.BaseDirectory, "PresentMon.exe"), Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon.exe") }.FirstOrDefault(File.Exists);
            if (pm is null) return Fail("PresentMon.exe not found");
            Directory.CreateDirectory(captures);
            var exe = proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? proc : proc + ".exe";
            var psi = new ProcessStartInfo(pm)
            {
                UseShellExecute = false, CreateNoWindow = true,
                ArgumentList = { "--process_name", exe, "--output_file", outFile, "--timed", secs.ToString(), "--terminate_after_timed", "--stop_existing_session", "--no_console_stats" },
            };
            using var p = Process.Start(psi)!;
            if (!p.WaitForExit((secs + 30) * 1000)) { p.Kill(); return Fail("timed out"); }
            return File.Exists(outFile) ? 0 : Fail("no output");
        }
        case "restore-point":
        {
            var desc = Arg(args, "--description") ?? "Framecalm";
            if (desc.Length > 64) desc = desc[..64];
            var info = new RESTOREPOINTINFO { dwEventType = 100 /*BEGIN_SYSTEM_CHANGE*/, dwRestorePtType = 12 /*MODIFY_SETTINGS*/, szDescription = desc };
            return SRSetRestorePointW(ref info, out var status) ? 0 : Fail($"restore point not created (status {status.nStatus}); System Restore may be off");
        }
        case "tweak":
        {
            var requests = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(captures)!, "requests"));
            var file = Path.GetFullPath(Arg(args, "--in") ?? "");
            if (!file.StartsWith(requests + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !file.EndsWith(".json")) return Fail("request must be in the requests folder");
            var result = Path.ChangeExtension(file, ".result.txt");
            var req = System.Text.Json.JsonSerializer.Deserialize<Framecalm.Windows.TweakRequest>(File.ReadAllText(file));
            if (req is null || req.Writes.Count == 0 || req.Writes.Count > 400) return Done(result, 2, "empty or oversized request");
            if (!Regex.IsMatch(req.UserSid, @"^S-1-5-21-[0-9\-]{6,80}$")) return Done(result, 2, "bad user id");
            foreach (var w in req.Writes)
                if (Framecalm.Windows.TweakIo.Validate(w) is { } why) return Done(result, 2, "Refused: " + why);
            var note = "";
            if (req.RestorePoint)
            {
                var info = new RESTOREPOINTINFO { dwEventType = 100, dwRestorePtType = 12, szDescription = "Before Framecalm changes" };
                note = SRSetRestorePointW(ref info, out _) ? "Restore point created." : "No restore point (System Restore is off or one was made recently).";
            }
            try { Framecalm.Windows.TweakIo.ApplyDirect(req.Writes, req.UserSid); }
            catch (Exception ex) { return Done(result, 2, "Windows refused a change: " + ex.Message); }
            return Done(result, 0, note);
        }
        default:
            return Fail("unknown command");
    }
}
catch (Exception ex) { return Fail(ex.Message); }

static int Fail(string msg) { Console.Error.WriteLine(msg); return 2; }

static int Done(string resultFile, int code, string msg) { try { File.WriteAllText(resultFile, msg); } catch { } return code; }

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct RESTOREPOINTINFO { public int dwEventType, dwRestorePtType; public long llSequenceNumber; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szDescription; }

[StructLayout(LayoutKind.Sequential)]
struct STATEMGRSTATUS { public int nStatus; public long llSequenceNumber; }

partial class Program
{
    [DllImport("srclient.dll", CharSet = CharSet.Unicode)]
    static extern bool SRSetRestorePointW(ref RESTOREPOINTINFO info, out STATEMGRSTATUS status);
}
