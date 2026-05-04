namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Buffers.Text;
using System.Text;
using System.Text.Json;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// Codifica e decodifica cursores opacos AES-GCM (ADR-0026): JSON do
/// <see cref="CursorPayload"/> cifrado via <see cref="IUniPlusEncryptionService"/>
/// e codificado em Base64URL.
/// </summary>
public sealed class CursorEncoder
{
    /// <summary>Nome de chave padrão usada para cifrar cursores.</summary>
    public const string DefaultKeyName = "cursor";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IUniPlusEncryptionService _encryption;
    private readonly string _keyName;
    private readonly TimeProvider _timeProvider;

    public CursorEncoder(IUniPlusEncryptionService encryption)
        : this(encryption, DefaultKeyName, TimeProvider.System)
    {
    }

    public CursorEncoder(IUniPlusEncryptionService encryption, string keyName, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _encryption = encryption;
        _keyName = keyName;
        _timeProvider = timeProvider;
    }

    public async Task<string> EncodeAsync(CursorPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, PayloadJsonOptions);
        byte[] ciphertext = await _encryption.EncryptAsync(_keyName, plaintext, cancellationToken).ConfigureAwait(false);
        return Base64Url.EncodeToString(ciphertext);
    }

    public async Task<CursorDecodeResult> TryDecodeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return CursorDecodeResult.Invalid();

        byte[] ciphertext;
        try
        {
            ciphertext = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException)
        {
            return CursorDecodeResult.Invalid();
        }

        byte[] plaintext;
        try
        {
            plaintext = await _encryption.DecryptAsync(_keyName, ciphertext, cancellationToken).ConfigureAwait(false);
        }
        catch (EncryptionFailureException)
        {
            return CursorDecodeResult.Invalid();
        }

        CursorPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CursorPayload>(plaintext, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return CursorDecodeResult.Invalid();
        }

        if (payload is null || string.IsNullOrEmpty(payload.After) || string.IsNullOrEmpty(payload.ResourceTag))
            return CursorDecodeResult.Invalid();

        if (payload.ExpiresAt <= _timeProvider.GetUtcNow())
            return CursorDecodeResult.Expired(payload);

        return CursorDecodeResult.Success(payload);
    }
}
