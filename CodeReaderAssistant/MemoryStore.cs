using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodeReaderAssistant;

public sealed class MemoryEntry
{
    public string Key { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MemoryStore
{
    private const string MemoryVersion = "local-v3";
    private readonly object sync = new();
    private readonly string filePath;
    private readonly List<MemoryEntry> entries;

    public MemoryStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeReaderAssistant");
        Directory.CreateDirectory(folder);
        filePath = Path.Combine(folder, "memory.json");
        entries = Load();
    }

    public MemoryEntry? Find(string input, AppMode mode, ProgrammingLanguage language)
    {
        var key = MakeKey(input, mode, language);
        lock (sync)
        {
            return entries.FirstOrDefault(entry => entry.Key == key) is { } entry
                ? Clone(entry)
                : null;
        }
    }

    public void SaveResult(string input, AppMode mode, ProgrammingLanguage language, string result)
    {
        var key = MakeKey(input, mode, language);
        lock (sync)
        {
            var entry = entries.FirstOrDefault(item => item.Key == key);
            if (entry is null)
            {
                entry = new MemoryEntry
                {
                    Key = key,
                    Mode = mode.ToString(),
                    Language = language.ToString(),
                    Input = input,
                };
                entries.Add(entry);
            }

            entry.Result = result;
            entry.UpdatedAtUtc = DateTime.UtcNow;
            Persist();
        }
    }

    public void SaveNote(string input, AppMode mode, ProgrammingLanguage language, string note)
    {
        var key = MakeKey(input, mode, language);
        lock (sync)
        {
            var entry = entries.FirstOrDefault(item => item.Key == key);
            if (entry is null)
            {
                entry = new MemoryEntry
                {
                    Key = key,
                    Mode = mode.ToString(),
                    Language = language.ToString(),
                    Input = input,
                };
                entries.Add(entry);
            }

            entry.Note = note;
            entry.UpdatedAtUtc = DateTime.UtcNow;
            Persist();
        }
    }

    private List<MemoryEntry> Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new List<MemoryEntry>();
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? new List<MemoryEntry>();
        }
        catch (Exception)
        {
            return new List<MemoryEntry>();
        }
    }

    private void Persist()
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = filePath + ".tmp";
        File.WriteAllText(temporaryPath, json, Encoding.UTF8);
        File.Move(temporaryPath, filePath, true);
    }

    private static MemoryEntry Clone(MemoryEntry entry) => new()
    {
        Key = entry.Key,
        Mode = entry.Mode,
        Language = entry.Language,
        Input = entry.Input,
        Result = entry.Result,
        Note = entry.Note,
        UpdatedAtUtc = entry.UpdatedAtUtc,
    };

    private static string MakeKey(string input, AppMode mode, ProgrammingLanguage language)
    {
        var normalized = $"{MemoryVersion}|{mode}|{language}|{input.Trim().Replace("\r\n", "\n")}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
}
