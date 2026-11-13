namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O instante de expiração do cursor e o TTL que o produz ficam em microssegundos inteiros, e o cursor vale até o
/// instante exato de expiração, exclusive.
/// </summary>
public sealed class CursorTtlEmMicrossegundosTests
{
    private static readonly string ChaveBase64 = Convert.ToBase64String(new byte[32]);
    private static readonly DateTimeOffset Emissao = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiracaoComSetimaCasa = Emissao.AddMinutes(15).AddTicks(1_234_567);
    private static readonly DateTimeOffset ExpiracaoEmMicrossegundos = Emissao.AddMinutes(15).AddTicks(1_234_560);

    private static CursorEncoder EncoderNoInstante(DateTimeOffset agora) => new(
        new LocalAesEncryptionService(
            Options.Create(new EncryptionOptions { Provider = "local", LocalKey = ChaveBase64 }),
            NullLogger<LocalAesEncryptionService>.Instance),
        Options.Create(new CursorPaginationOptions()),
        new RelogioFixo(agora));

    private static CursorPayload ComExpiracao(DateTimeOffset expiraEm) =>
        new("id", 10, "certames", expiraEm, PaginationDirection.Next);

    [Fact(DisplayName = "O TTL do cursor configurado com resto de ticks é guardado em microssegundos inteiros, sem arredondar")]
    public void CursorTtl_ConfiguradoComResto_ChegaEmMicrossegundosInteiros()
    {
        CursorPaginationOptions opcoes = new ConfigurationBuilder()
            .AddInMemoryCollection([new($"{CursorPaginationOptions.SectionName}:CursorTtl", "00:15:00.0000009")])
            .Build()
            .GetSection(CursorPaginationOptions.SectionName)
            .Get<CursorPaginationOptions>()!;

        opcoes.CursorTtl.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact(DisplayName = "O cursor emitido guarda a expiração em microssegundos inteiros, ainda que o instante traga a 7ª casa")]
    public async Task Emitir_ExpiracaoComSetimaCasa_GuardaEmMicrossegundos()
    {
        CursorEncoder encoder = EncoderNoInstante(Emissao);

        string cursor = await encoder.EncodeAsync(ComExpiracao(ExpiracaoComSetimaCasa));

        (await encoder.TryDecodeAsync(cursor)).Payload!.ExpiresAt.Should().Be(ExpiracaoEmMicrossegundos);
    }

    [Fact(DisplayName = "O cursor vale até um microssegundo antes da expiração")]
    public async Task Reapresentar_UmMicrossegundoAntesDaExpiracao_Vale()
    {
        string cursor = await EncoderNoInstante(Emissao).EncodeAsync(ComExpiracao(ExpiracaoComSetimaCasa));

        CursorDecodeResult resultado = await EncoderNoInstante(ExpiracaoEmMicrossegundos.AddTicks(-10)).TryDecodeAsync(cursor);

        resultado.Status.Should().Be(CursorDecodeStatus.Success);
    }

    [Fact(DisplayName = "O cursor reapresentado no instante exato de expiração está expirado")]
    public async Task Reapresentar_NoInstanteExatoDaExpiracao_Expirou()
    {
        string cursor = await EncoderNoInstante(Emissao).EncodeAsync(ComExpiracao(ExpiracaoComSetimaCasa));

        CursorDecodeResult resultado = await EncoderNoInstante(ExpiracaoEmMicrossegundos).TryDecodeAsync(cursor);

        resultado.Status.Should().Be(CursorDecodeStatus.Expired);
    }
}
