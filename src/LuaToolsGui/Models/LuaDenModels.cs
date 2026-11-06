using System.Text.Json.Serialization;

namespace LuaToolsGui.Models;

/// <summary>Top-level response from GET /api/files/games.</summary>
public sealed class LuaDenGamesResponse
{
    [JsonPropertyName("known")] public bool Known { get; set; }
    [JsonPropertyName("setting_up")] public bool SettingUp { get; set; }
    [JsonPropertyName("games")] public List<LuaDenGame> Games { get; set; } = [];
}

/// <summary>Read-only game metadata published by Playden's LAN companion API.</summary>
public class LuaDenGame
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("app")] public uint App { get; set; }
    [JsonPropertyName("drive")] public string Drive { get; set; } = "";
    [JsonPropertyName("drive_absent")] public bool DriveAbsent { get; set; }
    [JsonPropertyName("installed")] public bool Installed { get; set; }
    [JsonPropertyName("running")] public bool Running { get; set; }
    [JsonPropertyName("setting_up")] public bool SettingUp { get; set; }
    [JsonPropertyName("cloud")] public bool Cloud { get; set; }
    [JsonPropertyName("last_played")] public long? LastPlayed { get; set; }
    [JsonPropertyName("windows_user_bytes")] public ulong? WindowsUserBytes { get; set; }
    [JsonPropertyName("physx_needed")] public string? PhysxNeeded { get; set; }
}

/// <summary>Extended game response from GET /api/files/game.</summary>
public sealed class LuaDenGameDetails : LuaDenGame
{
    [JsonPropertyName("game_folder")] public string? GameFolder { get; set; }
    [JsonPropertyName("roots")] public List<LuaDenRoot> Roots { get; set; } = [];
    [JsonPropertyName("redist_busy")] public bool RedistBusy { get; set; }
    [JsonPropertyName("all_setting_up")] public bool AllSettingUp { get; set; }
}

/// <summary>A file-system root exposed for a game.</summary>
public sealed class LuaDenRoot
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("exists")] public bool Exists { get; set; }
}

/// <summary>Response from GET /api/files/list.</summary>
public sealed class LuaDenFileListing
{
    [JsonPropertyName("exists")] public bool Exists { get; set; }
    [JsonPropertyName("read_only")] public bool ReadOnly { get; set; }
    [JsonPropertyName("truncated")] public bool Truncated { get; set; }
    [JsonPropertyName("unshown")] public ulong Unshown { get; set; }
    [JsonPropertyName("entries")] public List<LuaDenFileEntry> Entries { get; set; } = [];
}

/// <summary>One directory or file from a Playden file listing.</summary>
public sealed class LuaDenFileEntry
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("directory")] public bool Directory { get; set; }
    [JsonPropertyName("size")] public ulong Size { get; set; }
    [JsonPropertyName("modified")] public long Modified { get; set; }
    [JsonPropertyName("read_only")] public bool ReadOnly { get; set; }
    [JsonPropertyName("recoverable")] public bool Recoverable { get; set; }
    [JsonPropertyName("previous")] public LuaDenPreviousVersion? Previous { get; set; }

    [JsonIgnore]
    public string KindLabel => Directory ? "Folder" : FormatSize(Size);

    [JsonIgnore]
    public string ModifiedLabel
    {
        get
        {
            try
            {
                return Modified > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(Modified).ToLocalTime().ToString("g")
                    : "";
            }
            catch (ArgumentOutOfRangeException) { return ""; }
        }
    }

    private static string FormatSize(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes:N0} {units[unit]}"
            : $"{size:N1} {units[unit]}";
    }
}

/// <summary>Metadata for the previous recoverable version of a file.</summary>
public sealed class LuaDenPreviousVersion
{
    [JsonPropertyName("size")] public ulong Size { get; set; }
    [JsonPropertyName("modified")] public long Modified { get; set; }
}

public sealed class LuaDenPairStatus
{
    [JsonPropertyName("paired")] public bool Paired { get; set; }
}

public sealed class LuaDenPairStart
{
    [JsonPropertyName("code_on_tv")] public bool CodeOnTv { get; set; }
}

public sealed class LuaDenPairConfirm
{
    [JsonPropertyName("paired")] public bool Paired { get; set; }
}

public sealed class LuaDenProfileImport
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("app")] public uint App { get; set; }
    [JsonPropertyName("pins")] public int Pins { get; set; }
}
