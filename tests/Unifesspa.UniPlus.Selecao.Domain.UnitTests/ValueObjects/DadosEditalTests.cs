namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
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

    [Theory(DisplayName = "O número do edital em branco é ausência de número")]
    [InlineData("")]
    [InlineData("   ")]
    public void DadosEdital_NumeroEmBranco_ViraNulo(string numero)
    {
        DadosEdital dados = DadosEdital.Criar(numero, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!;

        dados.Numero.Should().BeNull();
    }

    [Fact(DisplayName = "O mesmo documento duas vezes, ou como principal e como anexo, é recusado")]
    public void AnexoRepetido_Recusado()
    {
        Guid principal = Guid.CreateVersion7();
        AnexoDoEdital anexo = Anexo(Guid.CreateVersion7());

        Result<DadosEdital> repetido = Dados(principal, [anexo, anexo with { Id = Guid.CreateVersion7() }]);
        Result<DadosEdital> comoPrincipal = Dados(principal, [Anexo(principal)]);

        repetido.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.Repetido);
        comoPrincipal.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.Repetido);
    }

    [Fact(DisplayName = "Mais anexos que o limite são recusados")]
    public void AnexosAcimaDoLimite_Recusados()
    {
        AnexoDoEdital[] anexos = [.. Enumerable.Range(0, AnexoDoEdital.QuantidadeMaxima + 1).Select(static _ => Anexo(Guid.CreateVersion7()))];

        Dados(Guid.CreateVersion7(), anexos).Error!.Code.Should().Be(AnexoDoEditalErrorCodes.QuantidadeExcedida);
    }

    private static Result<DadosEdital> Dados(Guid principal, IReadOnlyList<AnexoDoEdital> anexos) =>
        DadosEdital.Criar("001/2026", InstanteEmBelem.Em(2026, 1, 1), InstanteEmBelem.Em(2026, 1, 31, 23, 59, 59), principal, anexos);

    private static AnexoDoEdital Anexo(Guid documentoId) =>
        new(Guid.CreateVersion7(), documentoId, "Quadro.pdf", FormatoDeDocumento.Pdf, 10, new string('a', 64));
}
