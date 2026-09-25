namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class CursorEncoderTests
{
    private static readonly byte[] ValidKey = new byte[32];
    private static readonly string ValidKeyBase64 = Convert.ToBase64String(ValidKey);

    private static (CursorEncoder Encoder, MutableTimeProvider Time) CriarEncoder(
        DateTimeOffset? now = null,
        string keyName = CursorPaginationOptions.DefaultKeyName)
    {
        MutableTimeProvider time = new(now ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        return (new CursorEncoder(CriarCifragem(), Options.Create(new CursorPaginationOptions { KeyName = keyName }), time), time);
    }

    private static LocalAesEncryptionService CriarCifragem() => new(
        Options.Create(new EncryptionOptions { Provider = "local", LocalKey = ValidKeyBase64 }),
        NullLogger<LocalAesEncryptionService>.Instance);

    private static CursorPayload PayloadValido() => new(
        "id", 10, "certames", DateTimeOffset.UtcNow.AddMinutes(5), PaginationDirection.Next);

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
        CursorPayload payload = new(
            "01HQ...id", 20, "editais", DateTimeOffset.UtcNow.AddMinutes(10), PaginationDirection.Prev);

        string token = await encoder.EncodeAsync(payload);
        CursorDecodeResult resultado = await encoder.TryDecodeAsync(token);

        resultado.Status.Should().Be(CursorDecodeStatus.Success);
        resultado.Payload!.After.Should().Be(payload.After);
        resultado.Payload.Limit.Should().Be(payload.Limit);
        resultado.Payload.ResourceTag.Should().Be(payload.ResourceTag);
        resultado.Payload.ExpiresAt.Should().BeCloseTo(payload.ExpiresAt, TimeSpan.FromMilliseconds(1));
        resultado.Payload.Direction.Should().Be(PaginationDirection.Prev);
    }

    [Fact]
    public async Task TryDecode_TokenAdulterado_RetornaInvalid()
    {
        (CursorEncoder encoder, _) = CriarEncoder();
        CursorPayload payload = new(
            "id", 10, "editais", DateTimeOffset.UtcNow.AddMinutes(5), PaginationDirection.Next);
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
        CursorPayload payload = new(
            "id", 10, "editais", baseTime.AddMinutes(5), PaginationDirection.Next);
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

    [Fact]
    public void Options_SemConfiguracao_UsamAChaveCanonica()
    {
        new CursorPaginationOptions().KeyName.Should().Be("uniplus-idempotency-aesgcm");
    }

    [Fact]
    public async Task Encode_SemConfiguracao_CifraComAChaveCanonica()
    {
        (CursorEncoder encoder, _) = CriarEncoder();

        string token = await encoder.EncodeAsync(PayloadValido());

        // A mesma cifragem abre o cursor quando recebe explicitamente o nome canônico.
        byte[] claro = await CriarCifragem().DecryptAsync(
            CursorPaginationOptions.DefaultKeyName, System.Buffers.Text.Base64Url.DecodeFromChars(token));
        claro.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TryDecode_CursorCifradoComOutraChave_RetornaInvalid()
    {
        (CursorEncoder origem, _) = CriarEncoder(keyName: CursorPaginationOptions.DefaultKeyName);
        (CursorEncoder portal, _) = CriarEncoder(keyName: "uniplus-portal-cursor-aesgcm");

        string cursorDaOrigem = await origem.EncodeAsync(PayloadValido());
        string cursorDaPortal = await portal.EncodeAsync(PayloadValido());

        (await portal.TryDecodeAsync(cursorDaOrigem)).Status.Should().Be(CursorDecodeStatus.Invalid,
            "quem não detém a chave da origem não abre nem reaproveita o cursor dela");
        (await origem.TryDecodeAsync(cursorDaPortal)).Status.Should().Be(CursorDecodeStatus.Invalid,
            "a origem não aceita cursor emitido com a chave de quem a compõe");
        (await portal.TryDecodeAsync(cursorDaPortal)).Status.Should().Be(CursorDecodeStatus.Success);
    }
}
