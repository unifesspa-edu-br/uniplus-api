namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Xunit;

using static ModeloDeFase;

public sealed class CronogramaDoProcessoTests
{
    private static (string? Campo, string Codigo)[] Pares(IEnumerable<FieldError> recusas) =>
        [.. recusas.Select(static r => (r.Field, r.Error.Code))];

    [Fact(DisplayName = "Código que aparece em duas fases é ambíguo e não localiza fase alguma")]
    public void CodigoAmbiguo_DuasFasesComOMesmoCodigo_NaoLocalizaFase()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("ANALISE", 1).Declarar(), new ModeloDeFase("ANALISE", 2).Declarar(), new ModeloDeFase("RESULTADO", 3).Declarar());

        cronograma.CodigoAmbiguo("ANALISE").Should().BeTrue();
        cronograma.FaseDeCodigo("ANALISE").Should().BeNull();
        cronograma.CodigoAmbiguo("RESULTADO").Should().BeFalse();
        cronograma.FaseDeCodigo("RESULTADO")!.Ordem.Should().Be(3);
        cronograma.FaseDeCodigo("AUSENTE").Should().BeNull();
    }

    [Fact(DisplayName = "A fase que ancora o período de inscrição é a de menor ordem entre as que coletam inscrição")]
    public void FaseQueAncoraOPeriodoDeInscricao_ElegeAMenorOrdem()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("REMANEJAMENTO", 5) { ColetaInscricao = true }.Declarar(),
            new ModeloDeFase("INSCRICAO", 2) { ColetaInscricao = true }.Declarar(),
            new ModeloDeFase("RESULTADO", 1).Declarar());

        cronograma.FaseQueAncoraOPeriodoDeInscricao!.Codigo.Should().Be("INSCRICAO");
        Cronograma(new ModeloDeFase("RESULTADO", 1).Declarar()).FaseQueAncoraOPeriodoDeInscricao.Should().BeNull();
    }

    [Fact(DisplayName = "As recusas das declarações saem na ordem das posições, com o campo prefixado pela fase")]
    public void RecusasDasDeclaracoes_PrefixamOCampoPelaPosicao()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("PRIMEIRA", 1).Declarar(),
            new ModeloDeFase("SEGUNDA", 2).Janela(5, 1).Declarar(),
            new ModeloDeFase("TERCEIRA", 3) { EmiteParecerIndividual = true }.Janela(5, 1).Declarar());

        Pares(cronograma.RecusasDasDeclaracoes()).Should().Equal(
            ("fases[1].fim", "FaseCronograma.JanelaInvertida"),
            ("fases[2].fim", "FaseCronograma.JanelaInvertida"),
            ("fases[2].emiteParecerIndividual", "FaseCronograma.ParecerIndividualSemResultado"));
    }

    [Fact(DisplayName = "Recusa sem campo vira o lugar da fase, sem ponto sobrando")]
    public void RecusasDasDeclaracoes_CampoNulo_ViraOLugarDaFase()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("PRIMEIRA", 1).Declarar(), FaseNoEditalSemJanela("SEGUNDA", 2));

        Pares(cronograma.RecusasDasDeclaracoes()).Should().Equal(("fases[1]", "FaseCronograma.JanelaObrigatoriaEmDataNoEdital"));
    }

    [Fact(DisplayName = "As recusas podem ser filtradas pelos códigos de erro que interessam")]
    public void RecusasDasDeclaracoes_FiltradasPorCodigo()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("PRIMEIRA", 1) { EmiteParecerIndividual = true }.Janela(5, 1).Declarar());

        Pares(cronograma.RecusasDasDeclaracoes(new HashSet<string> { "FaseCronograma.ParecerIndividualSemResultado" }))
            .Should().Equal(("fases[0].emiteParecerIndividual", "FaseCronograma.ParecerIndividualSemResultado"));
    }

    private static FaseDeclarada FaseNoEditalSemJanela(string codigo, int ordem)
    {
        ModeloDeFase modelo = new(codigo, ordem);
        return Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma.FaseDeclarada.Declarar(
            Guid.CreateVersion7(),
            modelo.Origem() with { OrigemData = Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemDataFase.NoEdital },
            modelo.Decisoes(),
            modelo.Publicacoes(),
            []);
    }
}
