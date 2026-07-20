using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MusiQL.Api.Spotify;

public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string payload);
}

public sealed class AesGcmTokenProtector(IOptions<SpotifyOptions> options) : ITokenProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly Lazy<byte[]> _key = new(() => DeriveKey(options.Value.TokenEncryptionKey));

    private byte[] Key => _key.Value;

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(Key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipher.CopyTo(payload, NonceSize + TagSize);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string payload)
    {
        var bytes = Convert.FromBase64String(payload);
        var nonce = bytes.AsSpan(0, NonceSize);
        var tag = bytes.AsSpan(NonceSize, TagSize);
        var cipher = bytes.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(Key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] DeriveKey(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Spotify:TokenEncryptionKey is not configured.");
        }

        if (TryDecodeBase64(configured, out var raw) && raw.Length == 32)
        {
            return raw;
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        var buffer = new byte[value.Length];
        if (Convert.TryFromBase64String(value, buffer, out var written))
        {
            bytes = buffer[..written];
            return true;
        }

        bytes = [];
        return false;
    }
}
