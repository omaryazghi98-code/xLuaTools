using System.Text.Json;
using LuaToolsGui.Models;
using Xunit;

namespace LuaToolsGui.Tests;

public sealed class LuaDenModelTests
{
    [Fact]
    public void GamesCatalog_DeserializesPlaydenFieldNames()
    {
        const string json = """
            {
              "known": true,
              "setting_up": false,
              "games": [
                {
                  "id": "steam-220",
                  "name": "Half-Life 2",
                  "kind": "steam",
                  "app": 220,
                  "drive": "console",
                  "drive_absent": false,
                  "installed": true,
                  "running": false,
                  "setting_up": false,
                  "cloud": true
                }
              ]
            }
            """;

        var catalog = JsonSerializer.Deserialize<LuaDenGamesResponse>(json);

        Assert.NotNull(catalog);
        Assert.True(catalog.Known);
        Assert.Single(catalog.Games);
        Assert.Equal("steam-220", catalog.Games[0].Id);
        Assert.Equal((uint)220, catalog.Games[0].App);
        Assert.True(catalog.Games[0].Installed);
    }

    [Fact]
    public void GameDetails_DeserializesAvailableRoots()
    {
        const string json = """
            {
              "id": "steam-220",
              "name": "Half-Life 2",
              "kind": "steam",
              "app": 220,
              "drive": "console",
              "drive_absent": false,
              "installed": true,
              "running": false,
              "setting_up": false,
              "cloud": false,
              "game_folder": "/data/playden/games/steam-220",
              "roots": [
                { "id": "game", "exists": true },
                { "id": "user", "exists": true }
              ],
              "redist": [],
              "redist_busy": false,
              "all_setting_up": false
            }
            """;

        var details = JsonSerializer.Deserialize<LuaDenGameDetails>(json);

        Assert.NotNull(details);
        Assert.Equal("/data/playden/games/steam-220", details.GameFolder);
        Assert.Equal(2, details.Roots.Count);
        Assert.Equal("game", details.Roots[0].Id);
        Assert.True(details.Roots[0].Exists);
    }

    [Fact]
    public void FileListing_DeserializesFoldersFilesAndTruncation()
    {
        const string json = """
            {
              "exists": true,
              "read_only": false,
              "truncated": true,
              "unshown": 3,
              "entries": [
                {
                  "name": "Saved Games",
                  "directory": true,
                  "size": 0,
                  "modified": 1793900000,
                  "read_only": false
                },
                {
                  "name": "profile.dat",
                  "directory": false,
                  "size": 2048,
                  "modified": 1793900000,
                  "read_only": true,
                  "recoverable": true,
                  "previous": { "size": 1024, "modified": 1793800000 }
                }
              ]
            }
            """;

        var listing = JsonSerializer.Deserialize<LuaDenFileListing>(json);

        Assert.NotNull(listing);
        Assert.True(listing.Exists);
        Assert.True(listing.Truncated);
        Assert.Equal((uint)3, listing.Unshown);
        Assert.Equal(2, listing.Entries.Count);
        Assert.True(listing.Entries[0].Directory);
        Assert.Equal("2.0 KB", listing.Entries[1].KindLabel);
        Assert.True(listing.Entries[1].Recoverable);
        Assert.Equal((ulong)1024, listing.Entries[1].Previous!.Size);
    }
}
