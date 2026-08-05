using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AspireWebAppTemplate.Infrastructure.Utilities;

/// <summary>
/// Provides AES-GCM authenticated encryption/decryption for connection strings.
/// </summary>
/// <remarks>
/// <para><b>Payload format (V1):</b></para>
/// <code>
/// [ 1-byte version | 4-byte nonceLen | nonce | 4-byte tagLen | tag | 4-byte ctLen | ciphertext ]
/// </code>
///
/// <para><b>Usage in appsettings.json:</b></para>
/// <code>
/// "ConnectionStrings": {
///     "DefaultConnection": "enc:BASE64_PAYLOAD_HERE"
/// }
/// </code>
///
/// <para><b>Usage in Program.cs:</b></para>
/// <code>
/// var connStr = SecureConnectionString.DecryptIfNeeded(
///     builder.Configuration.GetConnectionString("DefaultConnection"));
/// </code>
///
/// <para><b>Key management:</b></para>
/// <list type="bullet">
///   <item>Default: uses the placeholder key below (MUST be replaced for production)</item>
///   <item>Override: set <c>CONNSTRING_KEY</c> environment variable to a Base64-encoded 32-byte key</item>
/// </list>
///
/// <para><b>Generate a new key (run in C# Interactive or a console app):</b></para>
/// <code>
/// Console.WriteLine(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
/// </code>
/// </remarks>
public static class SecureConnectionString
{
    #region Constants

    // ⚠️ PLACEHOLDER KEY — Replace per project or override via CONNSTRING_KEY environment variable.
    // Generate a new key: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    private const string PlaceholderKeyBase64 = "REPLACE_WITH_YOUR_OWN_32_BYTE_BASE64_KEY_HERE==";

    /// <summary>AES-GCM nonce size (96-bit, recommended by NIST).</summary>
    private const int NonceSizeBytes = 12;

    /// <summary>AES-GCM authentication tag size (128-bit).</summary>
    private const int TagSizeBytes = 16;

    /// <summary>Payload format version identifier.</summary>
    private const byte PayloadVersion = 0x01;

    /// <summary>Prefix used to identify encrypted values in configuration.</summary>
    private const string EncryptedPrefix = "enc:";

    /// <summary>Environment variable name for key override.</summary>
    private const string KeyEnvironmentVariable = "CONNSTRING_KEY";

    #endregion

    #region Public API

    /// <summary>
    /// Encrypts a plaintext connection string into a Base64 payload using AES-GCM.
    /// </summary>
    /// <param name="plaintext">The plaintext connection string to encrypt.</param>
    /// <param name="aad">Optional associated data to bind context. Must match on decrypt.</param>
    /// <returns>Base64-encoded payload containing version, nonce, tag, and ciphertext.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plaintext"/> is null.</exception>
    public static string Encrypt(string plaintext, byte[]? aad = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, pt, ct, tag, aad);

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(PayloadVersion);
            bw.Write(nonce.Length);
            bw.Write(nonce);
            bw.Write(tag.Length);
            bw.Write(tag);
            bw.Write(ct.Length);
            bw.Write(ct);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts a Base64 payload (produced by <see cref="Encrypt"/>) back to the plaintext connection string.
    /// </summary>
    /// <param name="base64Payload">The Base64 payload containing the encrypted data.</param>
    /// <param name="aad">Optional associated data used during encryption. Must match.</param>
    /// <returns>The decrypted plaintext connection string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="base64Payload"/> is null.</exception>
    /// <exception cref="CryptographicException">Thrown on tampering, wrong key, or wrong AAD.</exception>
    /// <exception cref="InvalidOperationException">Thrown on unsupported payload version.</exception>
    public static string Decrypt(string base64Payload, byte[]? aad = null)
    {
        ArgumentNullException.ThrowIfNull(base64Payload);

        var payload = Convert.FromBase64String(base64Payload);
        using var ms = new MemoryStream(payload);
        using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        var version = br.ReadByte();
        if (version != PayloadVersion)
            throw new InvalidOperationException($"Unsupported payload version: {version}");

        var nonceLen = br.ReadInt32();
        var nonce = br.ReadBytes(nonceLen);

        var tagLen = br.ReadInt32();
        var tag = br.ReadBytes(tagLen);

        var ctLen = br.ReadInt32();
        var ct = br.ReadBytes(ctLen);

        var key = GetKey();
        var pt = new byte[ct.Length];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ct, tag, pt, aad); // Throws CryptographicException if tampered

        return Encoding.UTF8.GetString(pt);
    }

    /// <summary>
    /// Transparently decrypts a value if it starts with <c>"enc:"</c>, otherwise returns as-is.
    /// Use this in <c>Program.cs</c> or service registration to handle both encrypted and plaintext values.
    /// </summary>
    /// <param name="value">The configuration value (may or may not be encrypted).</param>
    /// <returns>The decrypted value if prefixed with <c>"enc:"</c>, or the original value unchanged.</returns>
    /// <example>
    /// <code>
    /// var connStr = SecureConnectionString.DecryptIfNeeded(
    ///     builder.Configuration.GetConnectionString("DefaultConnection"));
    /// </code>
    /// </example>
    public static string DecryptIfNeeded(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        if (value.StartsWith(EncryptedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var payload = value[EncryptedPrefix.Length..];
            return Decrypt(payload);
        }

        // Not encrypted — return as-is
        return value;
    }

    #endregion

    #region Key Management

    /// <summary>
    /// Retrieves the AES-256 key, preferring the <c>CONNSTRING_KEY</c> environment variable if present.
    /// The value must be Base64-encoded 32 bytes (256-bit).
    /// Falls back to the placeholder key otherwise.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the key is not valid Base64 or is not exactly 32 bytes.
    /// </exception>
    private static byte[] GetKey()
    {
        var overrideBase64 = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
        var b64 = string.IsNullOrWhiteSpace(overrideBase64) ? PlaceholderKeyBase64 : overrideBase64;

        byte[] key;
        try
        {
            key = Convert.FromBase64String(b64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"{KeyEnvironmentVariable} must be a valid Base64-encoded string.", ex);
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException(
                $"AES key must be exactly 32 bytes (256-bit). Actual: {key.Length} bytes.");
        }

        return key;
    }

    #endregion
}
