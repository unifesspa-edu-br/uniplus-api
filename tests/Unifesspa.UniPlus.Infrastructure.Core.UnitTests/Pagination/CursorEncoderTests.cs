namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

public sealed class CursorEncoderTests
{
    private static readonly byte[] ValidKey = new byte[32];
    private static readonly string ValidKeyBase64 = Convert.ToBase64String(ValidKey);

    private static (CursorEncoder Encoder, MutableTimeProvider Time) CriarEncoder(DateTimeOffset? now = null)
    {
        MutableTimeProvider time = new(now ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        LocalAesEncryptionService encryption = new(
            Options.Create(new EncryptionOptions { Provider = "local", LocalKey = ValidKeyBase64 }),
            NullLogger<LocalAesEncryptionService>.Instance);
        return (new CursorEncoder(encryption, CursorEncoder.DefaultKeyName, time), time);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public MutableTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span) => _now = _now.Add(span);
    }

    [Fact]
    public async Task Encode_Decode_FazRoundtrip()
    {
        (CursorEncoder encoder, _) = CriarEncoder();
        CursorPayload payload = new("01HQ...id", 20, "editais", DateTimeOffset.UtcNow.AddMinutes(10));

        string token = await encoder.EncodeAsync(payload);
        CursorDecodeResult resultado = await encoder.TryDecodeAsync(token);

        resultado.Status.Should().Be(CursorDecodeStatus.Success);
        resultado.Payload!.After.Should().Be(payload.After);
        resultado.Payload.Limit.Should().Be(payload.Limit);
        resultado.Payload.ResourceTag.Should().Be(payload.ResourceTag);
        resultado.Payload.ExpiresAt.Should().BeCloseTo(payload.ExpiresAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task TryDecode_TokenAdulterado_RetornaInvalid()
    {
        (CursorEncoder encoder, _) = CriarEncoder();
        CursorPayload payload = new("id", 10, "editais", DateTimeOffset.UtcNow.AddMinutes(5));
        string token = await encoder.EncodeAsync(payload);

        // Inverte um caractere central, preservando comprimento e alfabeto base64url.
        char[] chars = [.. token];
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';
        string adulterado = new(chars);

        CursorDecodeResult resultado = await encoder.TryDecodeAsync(adulterado);

        resultado.Status.Should().Be(CursorDecodeStatus.Invalid);
        resultado.Payload.Should().BeNull();
    }

    [Fact]
    public async Task TryDecode_TokenExpirado_RetornaExpired()
    {
        DateTimeOffset baseTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        (CursorEncoder encoder, MutableTimeProvider time) = CriarEncoder(baseTime);
        CursorPayload payload = new("id", 10, "editais", baseTime.AddMinutes(5));
        string token = await encoder.EncodeAsync(payload);

        time.Advance(TimeSpan.FromMinutes(10));
        CursorDecodeResult resultado = await encoder.TryDecodeAsync(token);

        resultado.Status.Should().Be(CursorDecodeStatus.Expired);
        resultado.Payload.Should().NotBeNull();
    }

    [Fact]
    public async Task TryDecode_TokenVazio_RetornaInvalid()
    {
        (CursorEncoder encoder, _) = CriarEncoder();

        CursorDecodeResult resultado = await encoder.TryDecodeAsync(string.Empty);

        resultado.Status.Should().Be(CursorDecodeStatus.Invalid);
    }

    [Fact]
    public async Task TryDecode_TokenNaoBase64Url_RetornaInvalid()
    {
        (CursorEncoder encoder, _) = CriarEncoder();

        CursorDecodeResult resultado = await encoder.TryDecodeAsync("@@@invalid@@@");

        resultado.Status.Should().Be(CursorDecodeStatus.Invalid);
    }
}
