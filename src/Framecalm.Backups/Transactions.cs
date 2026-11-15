using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Framecalm.Core;

namespace Framecalm.Backups;

/// <summary>Where journals, backups and history live. The real one is a folder; demo mode uses memory only.</summary>
public interface IStateStore
{
    string? ReadText(string name);
    void WriteText(string name, string text);           // durable: written through to disk
    void Delete(string name);
    IEnumerable<string> List(string prefix);
    void CopyIn(string sourceFile, string name);         // backup copy of an original file
    void CopyOut(string name, string destFile);          // restore from backup
    string? Hash(string name);
}

public sealed class FolderStateStore(string root) : IStateStore
{
    string P(string name) => Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));

    public string? ReadText(string name) => File.Exists(P(name)) ? File.ReadAllText(P(name)) : null;

    public void WriteText(string name, string text)
    {
        var path = P(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            fs.Write(bytes);
            fs.Flush(true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    public void Delete(string name) { if (File.Exists(P(name))) File.Delete(P(name)); }

    public IEnumerable<string> List(string prefix)
    {
        var dir = Path.GetDirectoryName(P(prefix + "x"))!;
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir).Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".tmp"));
    }

    public void CopyIn(string sourceFile, string name)
    {
        var dest = P(name);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(sourceFile, dest, overwrite: true);
        using var fs = new FileStream(dest, FileMode.Open, FileAccess.ReadWrite);
        fs.Flush(true);
    }

    public void CopyOut(string name, string destFile) => AtomicFile.Write(destFile, File.ReadAllBytes(P(name)));

    public string? Hash(string name) => File.Exists(P(name)) ? AtomicFile.Hash(File.ReadAllBytes(P(name))) : null;
}

public sealed class MemoryStateStore : IStateStore
{
    readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    public Func<string, byte[]>? ReadSource { get; set; }
    public Action<string, byte[]>? WriteDest { get; set; }

    public string? ReadText(string name) => _files.TryGetValue(name, out var b) ? Encoding.UTF8.GetString(b) : null;
    public void WriteText(string name, string text) => _files[name] = Encoding.UTF8.GetBytes(text);
    public void Delete(string name) => _files.Remove(name);
    public IEnumerable<string> List(string prefix) => _files.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
    public void CopyIn(string sourceFile, string name) => _files[name] = ReadSource?.Invoke(sourceFile) ?? File.ReadAllBytes(sourceFile);
    public void CopyOut(string name, string destFile) { if (WriteDest != null) WriteDest(destFile, _files[name]); else AtomicFile.Write(destFile, _files[name]); }
    public string? Hash(string name) => _files.TryGetValue(name, out var b) ? AtomicFile.Hash(b) : null;
}

public static class AtomicFile
{
    public static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b));
    public static string? HashFile(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;

    /// <summary>Write to a temp file next to the target, flush to disk, then swap it in. A crash leaves either the old or the new file, never half of one.</summary>
    public static void Write(string path, byte[] bytes)
    {
        var tmp = path + ".framecalm.tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            fs.Write(bytes);
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null, ignoreMetadataErrors: true);
        else File.Move(tmp, path);
    }
}

/// <summary>A config file edited through an adapter. Keeps the file's encoding and line endings.</summary>
public sealed class ConfigFileTarget(string path, IConfigAdapter adapter, GameDefinition def) : IChangeTarget
{
    public TargetKind Kind => TargetKind.ConfigFile;
    public string Path => path;
    public bool IsFile => true;
    public IConfigAdapter Adapter => adapter;
    public GameDefinition Definition => def;

    SettingDef Def(string key) => def.Settings.First(s => s.Key == key);

    public string Text() => File.ReadAllText(path);

    public string? Read(string key) => File.Exists(path) ? adapter.Get(File.ReadAllText(path), Def(key)) : null;

    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        var raw = File.ReadAllBytes(path);
        var bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
        var utf16 = raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE;
        var enc = utf16 ? Encoding.Unicode : new UTF8Encoding(bom);
        var text = enc.GetString(raw, utf16 ? 2 : bom ? 3 : 0, raw.Length - (utf16 ? 2 : bom ? 3 : 0));
        var check = adapter.Check(text, def);
        if (!check.Ok) throw new InvalidOperationException($"Config changed into an unknown format ({check.Reason}). Nothing was written.");
        foreach (var (k, v) in values) text = adapter.Set(text, Def(k), v ?? throw new InvalidOperationException("Config values can't be removed."));
        var body = enc.GetBytes(text);
        var pre = enc.GetPreamble();
        AtomicFile.Write(path, pre.Length > 0 && (bom || utf16) ? [.. pre, .. body] : body);
    }
}

public sealed class Journal
{
    public string BatchId { get; set; } = "";
    public string State { get; set; } = "pending";   // pending -> committed | rolledback
    public TargetKind Kind { get; set; }
    public string Target { get; set; } = "";
    public string? BackupName { get; set; }
    public string? OriginalHash { get; set; }
    public string? WrittenHash { get; set; }
    public List<ChangeRecord> Changes { get; set; } = [];
}

public sealed record ApplyResult(bool Ok, string Message, IReadOnlyList<ChangeRecord> Applied);

public sealed record Conflict(ChangeRecord Change, string? CurrentValue);

public sealed record UndoResult(int Undone, IReadOnlyList<Conflict> Conflicts, IReadOnlyList<string> Errors);

public enum ApplyStep { BackingUp, Writing, Verifying, Done }

/// <summary>
/// Every change goes through here: journal (durable) → backup original → write → read back and verify → commit.
/// If anything fails the original comes back. A journal left "pending" after a crash is rolled back on the next start.
/// </summary>
public sealed class TransactionManager(IStateStore store, Func<Journal, IChangeTarget?> resolve)
{
    const string HistoryName = "history.json";
    readonly object _lock = new();

    public List<ChangeRecord> History()
    {
        lock (_lock)
        {
            var t = store.ReadText(HistoryName);
            return t is null ? [] : JsonSerializer.Deserialize<List<ChangeRecord>>(t, Json.Options) ?? [];
        }
    }

    void SaveHistory(List<ChangeRecord> h) => store.WriteText(HistoryName, JsonSerializer.Serialize(h, Json.Options));

    void SaveJournal(Journal j) => store.WriteText($"journal/{j.BatchId}.json", JsonSerializer.Serialize(j, Json.Options));

    public ApplyResult Apply(IChangeTarget target, IReadOnlyList<ChangeRecord> changes, IProgress<ApplyStep>? progress = null)
    {
        if (changes.Count == 0) return new(true, "Nothing needed to change.", []);
        lock (_lock)
        {
            var batch = DateTime.Now.ToString("yyyyMMdd-HHmmss-") + Guid.NewGuid().ToString("N")[..6];
            var j = new Journal { BatchId = batch, Kind = target.Kind, Target = target.Path };
            foreach (var c in changes)
            {
                c.BatchId = batch;
                c.Kind = target.Kind;
                c.Target = target.Path;
                c.Status = ChangeStatus.Applied;
                c.AppliedAt = DateTime.Now;
                j.Changes.Add(c);
            }

            progress?.Report(ApplyStep.BackingUp);
            if (target.IsFile)
            {
                if (!File.Exists(target.Path)) return new(false, "The config file is missing. Launch the game once so it creates it, then try again.", []);
                j.BackupName = $"backups/{batch}/{Path.GetFileName(target.Path)}";
                store.CopyIn(target.Path, j.BackupName);
                j.OriginalHash = AtomicFile.HashFile(target.Path);
                if (store.Hash(j.BackupName) != j.OriginalHash)
                    return new(false, "Couldn't make a reliable backup copy, so nothing was changed.", []);
            }
            // current values must still be what we proposed from; if not, the user (or the game) changed something meanwhile
            foreach (var c in changes)
            {
                var now = target.Read(c.Key);
                if (!Same(now, c.Old))
                    return new(false, $"{c.Label} changed since we looked ({c.OldLabel} → now {now ?? "missing"}). Nothing was changed; review again.", []);
            }
            SaveJournal(j);

            progress?.Report(ApplyStep.Writing);
            try
            {
                target.Write(changes.Select(c => (c.Key, c.New)).ToList());
            }
            catch (Exception ex)
            {
                Rollback(j, target);
                return new(false, $"Couldn't write the changes ({ex.Message}). Your original is back in place.", []);
            }

            progress?.Report(ApplyStep.Verifying);
            var bad = changes.Where(c => !Same(target.Read(c.Key), c.New)).ToList();
            if (bad.Count > 0)
            {
                Rollback(j, target);
                return new(false, $"{bad[0].Label} didn't read back as {bad[0].NewLabel}, so everything was put back the way it was.", []);
            }

            if (target.IsFile) j.WrittenHash = AtomicFile.HashFile(target.Path);
            j.State = "committed";
            SaveJournal(j);
            var h = History();
            h.AddRange(changes);
            SaveHistory(h);
            progress?.Report(ApplyStep.Done);
            return new(true, $"{changes.Count} change{(changes.Count == 1 ? "" : "s")} applied and verified.", changes);
        }
    }

    void Rollback(Journal j, IChangeTarget target)
    {
        try
        {
            if (target.IsFile && j.BackupName != null) store.CopyOut(j.BackupName, target.Path);
            else target.Write(j.Changes.Select(c => (c.Key, c.Old)).ToList());
            j.State = "rolledback";
        }
        catch { j.State = "rollback-failed"; }
        SaveJournal(j);
    }

    /// <summary>Run on startup: anything left pending by a crash or power loss is put back.</summary>
    public List<string> Recover()
    {
        var notes = new List<string>();
        lock (_lock)
        {
            foreach (var name in store.List("journal/"))
            {
                Journal? j;
                try { j = JsonSerializer.Deserialize<Journal>(store.ReadText(name)!, Json.Options); }
                catch { continue; }
                if (j is null || j.State != "pending") continue;
                var target = resolve(j);
                if (target is null) { notes.Add($"Couldn't recover {j.Target}: its game is no longer installed."); continue; }
                if (target.IsFile && j.BackupName != null)
                {
                    var now = AtomicFile.HashFile(target.Path);
                    if (now != j.OriginalHash) store.CopyOut(j.BackupName, target.Path);
                }
                else
                {
                    target.Write(j.Changes.Select(c => (c.Key, c.Old)).ToList());
                }
                j.State = "rolledback";
                SaveJournal(j);
                notes.Add($"An unfinished change to {Path.GetFileName(j.Target)} was rolled back.");
            }
        }
        return notes;
    }

    /// <summary>Undo specific changes. A value the user changed by hand since is a conflict: we ask instead of overwriting,
    /// unless <paramref name="force"/> is set for those ids.</summary>
    public UndoResult Undo(IEnumerable<string> ids, ISet<string>? force = null, ISet<string>? keep = null)
    {
        lock (_lock)
        {
            var h = History();
            var wanted = ids.ToHashSet();
            var todo = h.Where(c => wanted.Contains(c.Id) && c.Status == ChangeStatus.Applied).ToList();
            var conflicts = new List<Conflict>();
            var errors = new List<string>();
            var undone = 0;
            foreach (var group in todo.GroupBy(c => (c.Kind, c.Target)))
            {
                var any = group.First();
                var target = resolve(new Journal { Kind = any.Kind, Target = any.Target, Changes = group.ToList() });
                if (target is null) { errors.Add($"{any.ScopeName}: target not found any more."); continue; }
                var writes = new List<ChangeRecord>();
                foreach (var c in group)
                {
                    if (keep?.Contains(c.Id) == true) { c.Status = ChangeStatus.Kept; continue; }
                    string? now;
                    try { now = target.Read(c.Key); }
                    catch (Exception ex) { errors.Add($"{c.Label}: {ex.Message}"); continue; }
                    if (Same(now, c.Old)) { c.Status = ChangeStatus.Undone; c.UndoneAt = DateTime.Now; undone++; continue; }
                    if (Same(now, c.New) || force?.Contains(c.Id) == true) writes.Add(c);
                    else conflicts.Add(new Conflict(c, now));
                }
                if (writes.Count == 0) continue;
                var reverse = writes.Select(c => new ChangeRecord
                {
                    Scope = c.Scope, ScopeName = c.ScopeName, Key = c.Key, Label = c.Label,
                    Old = target.Read(c.Key), New = c.Old, OldLabel = c.NewLabel, NewLabel = c.OldLabel,
                }).ToList();
                var r = ApplyRaw(target, reverse);
                if (r is null)
                {
                    foreach (var c in writes) { c.Status = ChangeStatus.Undone; c.UndoneAt = DateTime.Now; undone++; }
                }
                else errors.Add(r);
            }
            SaveHistory(h);
            return new(undone, conflicts, errors);
        }
    }

    /// <summary>Same journal/backup/verify path as Apply, without adding to history (used by undo).</summary>
    string? ApplyRaw(IChangeTarget target, List<ChangeRecord> reverse)
    {
        var batch = "undo-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-") + Guid.NewGuid().ToString("N")[..6];
        var j = new Journal { BatchId = batch, Kind = target.Kind, Target = target.Path, Changes = reverse };
        if (target.IsFile)
        {
            j.BackupName = $"backups/{batch}/{Path.GetFileName(target.Path)}";
            store.CopyIn(target.Path, j.BackupName);
            j.OriginalHash = AtomicFile.HashFile(target.Path);
        }
        SaveJournal(j);
        try { target.Write(reverse.Select(c => (c.Key, c.New)).ToList()); }
        catch (Exception ex) { Rollback(j, target); return ex.Message; }
        if (reverse.Any(c => !Same(target.Read(c.Key), c.New))) { Rollback(j, target); return "Undo didn't verify, so the file was left as it was."; }
        j.State = "committed";
        SaveJournal(j);
        return null;
    }

    public static bool Same(string? a, string? b)
    {
        if (a is null || b is null) return a is null && b is null;
        a = a.Trim().Trim('"'); b = b.Trim().Trim('"');
        if (double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
            return Math.Abs(x - y) < 1e-6;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
