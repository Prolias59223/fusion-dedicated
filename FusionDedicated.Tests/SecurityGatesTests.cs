using BonelabServerBrowser.Fusion;
using FusionDedicated;
using FusionDedicated.Server;
using FusionDedicated.Protocol;

namespace FusionDedicated.Tests;

public class SecurityGatesTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"fusion-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileNameWithoutExtension(_path) + "*"))
        {
            File.Delete(file);
        }
    }

    // ---- privacy gate ----

    [Fact]
    public void Public_and_private_lobbies_admit_everyone()
    {
        Assert.Null(ServerProtocol.PrivacyRefusal(0, 1, _ => false));
        Assert.Null(ServerProtocol.PrivacyRefusal(1, 1, _ => false));
    }

    [Fact]
    public void Friends_only_admits_only_friends()
    {
        Assert.Null(ServerProtocol.PrivacyRefusal(2, 1, _ => true));
        Assert.NotNull(ServerProtocol.PrivacyRefusal(2, 1, _ => false));
    }

    [Fact]
    public void Locked_lobbies_admit_nobody_not_even_friends()
    {
        Assert.NotNull(ServerProtocol.PrivacyRefusal(3, 1, _ => true));
    }

    // ---- connection request limits ----

    private static ServerProtocol.ConnectionRequest Request(
        Dictionary<string, string>? metadata = null,
        List<string>? equipped = null)
        => new(76561198, new Version(1, 14, 2), "fa534c5a.SomeAvatar",
            new byte[FusionProtocol.AvatarStatsSize],
            metadata ?? new Dictionary<string, string> { ["Username"] = "alice" },
            equipped ?? new List<string>());

    [Fact]
    public void A_ordinary_request_is_within_limits()
    {
        Assert.True(ServerProtocol.ConnectionRequestWithinLimits(Request(), messageBytes: 2048));
    }

    [Fact]
    public void An_oversized_request_is_refused_whole()
    {
        Assert.False(ServerProtocol.ConnectionRequestWithinLimits(
            Request(), messageBytes: ServerProtocol.MaxConnectionRequestBytes + 1));
    }

    [Fact]
    public void Metadata_that_is_too_plentiful_or_too_wordy_is_refused()
    {
        var manyKeys = Enumerable.Range(0, ServerProtocol.MaxMetadataEntries + 1)
            .ToDictionary(i => $"key{i}", i => "v");

        var longValue = new Dictionary<string, string>
        {
            ["Username"] = new string('x', ServerProtocol.MaxMetadataStringLength + 1),
        };

        Assert.False(ServerProtocol.ConnectionRequestWithinLimits(Request(manyKeys), 1024));
        Assert.False(ServerProtocol.ConnectionRequestWithinLimits(Request(longValue), 1024));
    }

    [Fact]
    public void Equipped_lists_that_are_too_long_or_barcode_like_are_refused()
    {
        var many = Enumerable.Range(0, ServerProtocol.MaxEquippedItems + 1)
            .Select(i => $"barcode:{i}").ToList();

        var longItem = new List<string> { new string('x', ServerProtocol.MaxBarcodeLength + 1) };

        Assert.False(ServerProtocol.ConnectionRequestWithinLimits(Request(equipped: many), 1024));
        Assert.False(ServerProtocol.ConnectionRequestWithinLimits(Request(equipped: longItem), 1024));
    }

    // ---- metadata message parsing ----

    [Fact]
    public void Player_metadata_messages_parse_and_reject_foreign_tags()
    {
        var message = ServerProtocol.WritePlayerMetadataResponse(3, FusionServer.PermissionMetadataKey, "OWNER");

        var parsed = ServerProtocol.TryReadPlayerMetadataResponse(message);

        Assert.NotNull(parsed);
        Assert.Equal((byte)3, parsed!.Value.Target);
        Assert.Equal(FusionServer.PermissionMetadataKey, parsed!.Value.Key);
        Assert.Equal("OWNER", parsed!.Value.Value);

        Assert.Null(ServerProtocol.TryReadPlayerMetadataResponse(new byte[] { 200, 0, 0 }));
    }

    // ---- corrupt config protection ----

    [Fact]
    public void A_corrupt_config_is_backed_up_before_defaults_replace_it()
    {
        File.WriteAllText(_path, "{ torn write");

        var config = ServerConfig.Load(_path);

        Assert.Equal("Dedicated Fusion Server", config.ServerName);

        var backups = Directory.GetFiles(Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(_path) + "*.corrupt-*");
        Assert.Single(backups);
    }
}
