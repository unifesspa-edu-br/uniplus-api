namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// A projeção pública lê os instantes do envelope na forma canônica de seis casas e devolve ao contrato o
/// instante inteiro, com os microssegundos; o instante em outra forma recusa a projeção do certame.
/// </summary>
public sealed class InstantesDaProjecaoDoCertameTests
{
    private static readonly DateTimeOffset InicioEmMicrossegundos = new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero).AddTicks(1234560);

    [Fact(DisplayName = "O período de inscrição devolve os microssegundos do envelope")]
    public void Periodo_RepassaOsMicrossegundos()
    {
        JsonObject envelope = EnvelopeDeReferencia();
        envelope["periodo"]!["inicio"] = "2026-01-01T03:00:00.123456Z";

        Result<CertamePublicadoDto> resultado = Projetar(envelope);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Periodo.Inicio.Should().Be(InicioEmMicrossegundos);
    }

    [Fact(DisplayName = "A janela da fase devolve os microssegundos do envelope")]
    public void JanelaDaFase_RepassaOsMicrossegundos()
    {
        JsonObject envelope = EnvelopeDeReferencia();
        envelope["cronogramaFases"]!["fases"]!.AsArray()[0]!["inicio"] = "2026-01-01T03:00:00.123456Z";

        Result<CertamePublicadoDto> resultado = Projetar(envelope);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.CronogramaFases[0].Inicio.Should().Be(InicioEmMicrossegundos);
    }

    [Fact(DisplayName = "A janela da etapa devolve os microssegundos do envelope")]
    public void JanelaDaEtapa_RepassaOsMicrossegundos()
    {
        JsonObject envelope = EnvelopeDeReferencia();
        envelope["etapas"]!.AsArray()[0]!["inicio"] = "2026-01-01T03:00:00.123456Z";

        Result<CertamePublicadoDto> resultado = Projetar(envelope);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Etapas[0].Inicio.Should().Be(InicioEmMicrossegundos);
    }

    [Theory(DisplayName = "O período de inscrição em outra forma que a de seis casas recusa a projeção")]
    [InlineData("2026-01-01T03:00:00Z")]
    [InlineData("2026-01-01T03:00:00.000Z")]
    [InlineData("2026-01-01T03:00:00.1234567Z")]
    [InlineData("2026-01-01T03:00:00.000000+00:00")]
    [InlineData("2026-01-01T00:00:00.000000-03:00")]
    public void Periodo_EmOutraForma_Recusa(string instante)
    {
        JsonObject envelope = EnvelopeDeReferencia();
        envelope["periodo"]!["inicio"] = instante;

        Projetar(envelope).IsFailure.Should().BeTrue();
    }

    [Theory(DisplayName = "A janela da fase em outra forma que a de seis casas recusa a projeção")]
    [InlineData("2026-01-01T03:00:00Z")]
    [InlineData("2026-01-01T03:00:00.000000+00:00")]
    public void JanelaDaFase_EmOutraForma_Recusa(string instante)
    {
        JsonObject envelope = EnvelopeDeReferencia();
        envelope["cronogramaFases"]!["fases"]!.AsArray()[0]!["fim"] = instante;

        Projetar(envelope).IsFailure.Should().BeTrue();
    }

    [Theory(DisplayName = "A janela da etapa em outra forma que a de seis casas recusa a projeção")]
    [InlineData("2026-01-01T03:00:00Z")]
    [InlineData("2026-01-01T03:00:00.000000+00:00")]
    public void JanelaDaEtapa_EmOutraForma_Recusa(string instante)
    {
        JsonObject envelope = EnvelopeDeReferencia();
        envelope["etapas"]!.AsArray()[0]!["inicio"] = instante;

        Projetar(envelope).IsFailure.Should().BeTrue();
    }

    private static JsonObject EnvelopeDeReferencia() =>
        (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(EnvelopeProjetavelDeTeste.Bytes))!;

    private static Result<CertamePublicadoDto> Projetar(JsonObject envelope) =>
        ProjecaoDoCertamePublicado.Projetar(
            Guid.CreateVersion7(), new AtoDaDivulgacao(Guid.CreateVersion7(), 2026, "EDITAL"), "Certame de teste", new string('a', 64),
            envelope, chave => new Uri($"https://acervo.teste/{chave}"));
}
