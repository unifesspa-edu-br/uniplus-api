namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O período de inscrição passou de <c>DateOnly</c> a instante (issue #1350), e com isso duas
/// regras que viviam nos validators de entrada migraram para cá: recusar o período invertido e
/// recusar o instante zerado.
/// </summary>
/// <remarks>
/// A migração não é organizacional. O value object é o ponto por onde passam os DOIS caminhos que
/// produzem um período — a publicação, que projeta da fase do cronograma, e a decodificação do
/// envelope, que reidrata uma versão publicada —, enquanto o validator só cobria o primeiro.
/// </remarks>
public sealed class DadosEditalTests
{
    [Fact(DisplayName = "Fim do período anterior ao início é recusado")]
    public void PeriodoInvertido_Recusado()
    {
        Result<DadosEdital> resultado = DadosEdital.Criar(
            "001/2026",
            InstanteEmBelem.Em(2026, 2, 1),
            InstanteEmBelem.Em(2026, 1, 2, 23, 59, 59),
            Guid.CreateVersion7());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DadosEdital.PeriodoInscricaoInvalido");
    }

    [Theory(DisplayName = "Instante zerado é recusado em qualquer um dos extremos")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PeriodoZerado_Recusado(bool inicioZerado, bool fimZerado)
    {
        Result<DadosEdital> resultado = DadosEdital.Criar(
            "001/2026",
            inicioZerado ? default : InstanteEmBelem.Em(2026, 1, 1),
            fimZerado ? default : InstanteEmBelem.Em(2026, 1, 31, 23, 59, 59),
            Guid.CreateVersion7());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DadosEdital.PeriodoInscricaoObrigatorio");
    }

    [Fact(DisplayName = "O período é normalizado para UTC, qualquer que seja o offset informado")]
    public void Periodo_NormalizadoParaUtc()
    {
        Result<DadosEdital> resultado = DadosEdital.Criar(
            "001/2026",
            InstanteEmBelem.Em(2026, 1, 1),
            InstanteEmBelem.Em(2026, 1, 31, 23, 59, 59),
            Guid.CreateVersion7());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.PeriodoInscricaoInicio.Offset.Should().Be(TimeSpan.Zero);
        resultado.Value.PeriodoInscricaoInicio.Should().Be(new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "O dia de referência legal sai do fuso institucional, não de UTC")]
    public void DiaDeReferenciaLegal_UsaOFusoInstitucional()
    {
        // Meia-noite em Belém é 03:00 UTC do MESMO dia; mas um certame cuja inscrição abre às 22h
        // de Belém já é o dia seguinte em UTC. É esse o caso que separa as duas leituras.
        DadosEdital dados = DadosEdital.Criar(
            "001/2026",
            InstanteEmBelem.Em(2026, 3, 10, 22, 0, 0),
            InstanteEmBelem.Em(2026, 3, 31, 23, 59, 59),
            Guid.CreateVersion7()).Value!;

        // O mesmo instante, em UTC, cai em 11/03.
        dados.PeriodoInscricaoInicio.UtcDateTime.Day.Should().Be(11);

        TimeZoneInfo belem = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId);

        dados.DiaDeReferenciaLegal(belem).Should().Be(new DateOnly(2026, 3, 10));
    }
}
