using Microsoft.Extensions.Options;
using MusiQL.Api.Spotify;
using Xunit;

namespace MusiQL.Tests.Spotify;

public class TokenProtectorTests
{
    private static AesGcmTokenProtector Protector(string key) =>
        new(Options.Create(new SpotifyOptions { TokenEncryptionKey = key }));

    [Fact]
    public void Round_trips_a_token()
    {
        var protector = Protector(Convert.ToBase64String(new byte[32]));
        var token = "BQC-example-access-token";

        var cipher = protector.Protect(token);

        Assert.NotEqual(token, cipher);
        Assert.Equal(token, protector.Unprotect(cipher));
    }

    [Fact]
    public void Produces_distinct_ciphertexts_per_call()
    {
        var protector = Protector("a-passphrase-that-gets-hashed-into-a-key");

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }
}
