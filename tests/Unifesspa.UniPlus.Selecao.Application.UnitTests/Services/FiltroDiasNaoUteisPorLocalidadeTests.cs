namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// CA-05 (issue #1113): a regra de abrangência decidida em 12/08/2026 — a localidade que
/// governa a contagem de dias úteis é a da unidade administradora do processo, não a de
/// cada campus de oferta.
/// </summary>
public sealed class FiltroDiasNaoUteisPorLocalidadeTests
{
    private static readonly DiaNaoUtilView FeriadoNacional = new(new DateOnly(2026, 9, 7), "NACIONAL", null, null);
    private static readonly DiaNaoUtilView RecessoInstitucional = new(new DateOnly(2026, 12, 24), "INSTITUCIONAL", null, null);
    private static readonly DiaNaoUtilView FeriadoEstadualPa = new(new DateOnly(2026, 8, 15), "ESTADUAL", null, "PA");
    private static readonly DiaNaoUtilView FeriadoMunicipalMaraba = new(new DateOnly(2026, 6, 15), "MUNICIPAL", "1504208", null);

    [Fact(DisplayName = "CA-05 (discriminador central): feriado MUNICIPAL do MESMO município da unidade administradora entra no filtro")]
    public void Filtrar_FeriadoMunicipalDoMesmoMunicipio_Entra()
    {
        IReadOnlyCollection<DateOnly> resultado = FiltroDiasNaoUteisPorLocalidade.Filtrar(
            [FeriadoMunicipalMaraba], cidadeCodigoIbge: "1504208", cidadeUf: "PA");

        resultado.Should().Contain(FeriadoMunicipalMaraba.Data);
    }

    [Fact(DisplayName = "CA-05 (discriminador central): o MESMO feriado MUNICIPAL, para OUTRO município, não entra")]
    public void Filtrar_FeriadoMunicipalDeOutroMunicipio_NaoEntra()
    {
        // Belém (1501402), não Marabá (1504208) — o feriado municipal cadastrado não é do
        // município da unidade administradora deste processo.
        IReadOnlyCollection<DateOnly> resultado = FiltroDiasNaoUteisPorLocalidade.Filtrar(
            [FeriadoMunicipalMaraba], cidadeCodigoIbge: "1501402", cidadeUf: "PA");

        resultado.Should().NotContain(FeriadoMunicipalMaraba.Data);
    }

    [Fact(DisplayName = "NACIONAL entra independente de cidade — inclusive sem cidade cadastrada")]
    public void Filtrar_Nacional_EntraSempre()
    {
        FiltroDiasNaoUteisPorLocalidade.Filtrar([FeriadoNacional], cidadeCodigoIbge: "1504208", cidadeUf: "PA")
            .Should().Contain(FeriadoNacional.Data);
        FiltroDiasNaoUteisPorLocalidade.Filtrar([FeriadoNacional], cidadeCodigoIbge: null, cidadeUf: null)
            .Should().Contain(FeriadoNacional.Data);
    }

    [Fact(DisplayName = "INSTITUCIONAL entra independente de cidade — recesso da Unifesspa, sem correspondência em calendário civil")]
    public void Filtrar_Institucional_EntraSempre()
    {
        FiltroDiasNaoUteisPorLocalidade.Filtrar([RecessoInstitucional], cidadeCodigoIbge: "1504208", cidadeUf: "PA")
            .Should().Contain(RecessoInstitucional.Data);
        FiltroDiasNaoUteisPorLocalidade.Filtrar([RecessoInstitucional], cidadeCodigoIbge: null, cidadeUf: null)
            .Should().Contain(RecessoInstitucional.Data);
    }

    [Fact(DisplayName = "ESTADUAL entra quando a UF bate, não entra quando não bate")]
    public void Filtrar_Estadual_DependeDaUf()
    {
        FiltroDiasNaoUteisPorLocalidade.Filtrar([FeriadoEstadualPa], cidadeCodigoIbge: "1504208", cidadeUf: "PA")
            .Should().Contain(FeriadoEstadualPa.Data);
        FiltroDiasNaoUteisPorLocalidade.Filtrar([FeriadoEstadualPa], cidadeCodigoIbge: "1501402", cidadeUf: "MA")
            .Should().NotContain(FeriadoEstadualPa.Data);
    }

    [Fact(DisplayName = "Sem cidade cadastrada na unidade administradora (snapshot pré-#1114), só NACIONAL/INSTITUCIONAL contam")]
    public void Filtrar_SemCidade_SoNacionalEInstitucionalContam()
    {
        IReadOnlyCollection<DateOnly> resultado = FiltroDiasNaoUteisPorLocalidade.Filtrar(
            [FeriadoNacional, RecessoInstitucional, FeriadoEstadualPa, FeriadoMunicipalMaraba],
            cidadeCodigoIbge: null, cidadeUf: null);

        resultado.Should().BeEquivalentTo([FeriadoNacional.Data, RecessoInstitucional.Data]);
    }

    [Fact(DisplayName = "Abrangência desconhecida falha explicitamente — nunca vira dia útil em silêncio")]
    public void Filtrar_AbrangenciaDesconhecida_Lanca()
    {
        DiaNaoUtilView invalido = new(new DateOnly(2026, 6, 15), "REGIONAL", null, null);

        Action act = () => FiltroDiasNaoUteisPorLocalidade.Filtrar([invalido], cidadeCodigoIbge: null, cidadeUf: null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "CA-05 (composição Filtrar → ResolverFimDaInterposicao): o mesmo feriado municipal muda o prazo para a cidade correspondente e não muda para outra")]
    public void FiltrarEResolver_FeriadoMunicipal_MudaPrazoSoParaACidadeCorrespondente()
    {
        // Segunda-feira 2026-06-15 é o único dia não útil MUNICIPAL cadastrado (Marabá).
        // 1 dia útil a partir de sexta 2026-06-12 (14h): sem contar 15/06 como não útil,
        // cai na própria segunda 15/06 (pula sáb/dom); contando-o, empurra pra terça 16/06.
        ArgsRegraPrazoRecurso args = new(
            PrazoValor: 1m,
            PrazoUnidade: UnidadePrazo.DiasUteis,
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null);
        DateTimeOffset publicacao = new(2026, 6, 12, 14, 0, 0, TimeSpan.Zero);

        IReadOnlyCollection<DateOnly> diasNaoUteisMaraba = FiltroDiasNaoUteisPorLocalidade.Filtrar(
            [FeriadoMunicipalMaraba], cidadeCodigoIbge: "1504208", cidadeUf: "PA");
        IReadOnlyCollection<DateOnly> diasNaoUteisBelem = FiltroDiasNaoUteisPorLocalidade.Filtrar(
            [FeriadoMunicipalMaraba], cidadeCodigoIbge: "1501402", cidadeUf: "PA");

        DateTimeOffset resultadoMaraba = args.ResolverFimDaInterposicao(publicacao, diasNaoUteisMaraba);
        DateTimeOffset resultadoBelem = args.ResolverFimDaInterposicao(publicacao, diasNaoUteisBelem);

        resultadoMaraba.Should().Be(new DateTimeOffset(2026, 6, 16, 14, 0, 0, TimeSpan.Zero));
        resultadoBelem.Should().Be(new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero));
        resultadoMaraba.Should().NotBe(resultadoBelem, "o feriado municipal de Marabá só altera o prazo de um processo cuja unidade administradora é de Marabá");
    }
}
