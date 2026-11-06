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

/// <summary>A file-system root exposed for a game. This first client milestone only displays it.</summary>
public sealed class LuaDenRoot
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("exists")] public bool Exists { get; set; }
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
