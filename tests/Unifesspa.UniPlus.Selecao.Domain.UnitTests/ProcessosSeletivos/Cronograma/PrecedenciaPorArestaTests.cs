namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static ModeloDeFase;

public sealed class PrecedenciaPorArestaTests
{
    private static ArestaPrecedencia Aresta(string antecessora, string sucessora, bool permiteSobreposicao = true) =>
        new(antecessora, sucessora, permiteSobreposicao);

    private static (string? Campo, string Codigo)[] Precedencia(CronogramaDoProcesso cronograma, params ArestaPrecedencia[] arestas) =>
        [.. cronograma.ConferirPrecedencia(arestas).Select(static r => (r.Field, r.Error.Code))];

    [Fact(DisplayName = "Antecessora com ordem igual ou maior que a da sucessora viola a precedência na sucessora")]
    public void ConferirPrecedencia_OrdemInvertida_RecusaNaSucessora()
    {
        CronogramaDoProcesso cronograma = Cronograma(new ModeloDeFase("A", 2).Declarar(), new ModeloDeFase("B", 1).Declarar());

        Precedencia(cronograma, Aresta("A", "B")).Should().Equal(("fases[1].ordem", "ProcessoSeletivo.PrecedenciaFaseViolada"));
    }

    [Fact(DisplayName = "A precedência violada cita cada fase pelo nome, entre aspas e com a ordem, e nunca pelo código")]
    public void ConferirPrecedencia_OrdemInvertida_CitaONomeEAOrdem()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            (new ModeloDeFase("A", 2) with { Nome = "Primeira fase" }).Declarar(), (new ModeloDeFase("B", 1) with { Nome = "Segunda fase" }).Declarar());

        cronograma.ConferirPrecedencia([Aresta("A", "B")]).Single().Error.Message.Should().Be(
            "A fase 'Primeira fase' (ordem 2) precede 'Segunda fase' (ordem 1) — a ordem declarada viola a precedência do cadastro.");
    }

    [Fact(DisplayName = "Aresta com fase ausente do cronograma não se aplica")]
    public void ConferirPrecedencia_FaseAusente_NaoSeAplica()
    {
        Precedencia(Cronograma(new ModeloDeFase("A", 1).Declarar()), Aresta("A", "B")).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sobreposição recusa quando o fim da antecessora passa do início da sucessora e o cadastro não permite")]
    public void ConferirPrecedencia_Sobreposicao_RecusaNoInicioDaSucessora()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 1).Janela(0, 10).Declarar(), new ModeloDeFase("B", 2).Janela(9, 20).Declarar());

        Precedencia(cronograma, Aresta("A", "B", permiteSobreposicao: false))
            .Should().Equal(("fases[1].inicio", "ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida"));
        cronograma.ConferirPrecedencia([Aresta("A", "B", permiteSobreposicao: false)]).Single().Error.Message.Should()
            .Be("O período desta fase coincide em parte com o da fase anterior, e o cadastro não permite essa coincidência.");
        Precedencia(cronograma, Aresta("A", "B", permiteSobreposicao: true)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Janelas que se tocam no mesmo instante não se sobrepõem")]
    public void ConferirPrecedencia_JanelasQueSeTocam_NaoSobrepoem()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 1).Janela(0, 10).Declarar(), new ModeloDeFase("B", 2).Janela(10, 20).Declarar());

        Precedencia(cronograma, Aresta("A", "B", permiteSobreposicao: false)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Janela invertida corta a sobreposição da aresta, e a recusa da janela fica na declaração")]
    public void ConferirPrecedencia_JanelaInvertida_CortaASobreposicao()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 1).Janela(12, 0).Declarar(), new ModeloDeFase("B", 2).Janela(5, 20).Declarar());

        Precedencia(cronograma, Aresta("A", "B", permiteSobreposicao: false)).Should().BeEmpty();
        cronograma.RecusasDasDeclaracoes().Should().ContainSingle().Which.Error.Code.Should().Be("FaseCronograma.JanelaInvertida");
    }

    [Fact(DisplayName = "Código repetido suspende só as arestas dele, e a aresta independente violada continua recusada")]
    public void ConferirPrecedencia_ColisaoDeCodigo_SuspendeSoAsArestasDoCodigo()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 1).Declarar(),
            new ModeloDeFase("A", 3).Declarar(),
            new ModeloDeFase("B", 5).Declarar(),
            new ModeloDeFase("C", 4).Declarar());
        ArestaPrecedencia[] arestas = [Aresta("A", "B"), Aresta("B", "C")];

        Precedencia(cronograma, arestas).Should().Equal(("fases[3].ordem", "ProcessoSeletivo.PrecedenciaFaseViolada"));
        cronograma.ConferirEstrutura().Should().ContainSingle()
            .Which.Field.Should().Be("fases[1].faseCanonicaId");
    }

    [Fact(DisplayName = "Código repetido pela mesma origem também suspende a aresta e tem uma causa de estrutura só")]
    public void ConferirPrecedencia_MesmaOrigem_UmaCausaDeEstruturaEArestaSuspensa()
    {
        Guid origem = Guid.CreateVersion7();
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 1) { OrigemId = origem }.Declarar(),
            new ModeloDeFase("A", 2) { OrigemId = origem }.Declarar(),
            new ModeloDeFase("B", 0).Declarar());

        Precedencia(cronograma, Aresta("A", "B")).Should().BeEmpty();
        cronograma.ConferirEstrutura().Should().ContainSingle();
    }

    [Fact(DisplayName = "Toda aresta suspensa envolve código que a estrutura acusa como repetido")]
    public void ConferirPrecedencia_ArestaSuspensaEnvolveCodigoAcusadoPelaEstrutura()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 9).Declarar(), new ModeloDeFase("A", 8).Declarar(), new ModeloDeFase("B", 1).Declarar());
        ArestaPrecedencia[] arestas = [Aresta("A", "B"), Aresta("B", "A")];

        string[] codigosAcusados =
        [
            .. cronograma.ConferirEstrutura()
                .Where(static r => r.Error.Code == "ProcessoSeletivo.FaseCanonicaDuplicada")
                .Select(r => cronograma.Fases[int.Parse(r.Field!.Split('[', ']')[1], System.Globalization.CultureInfo.InvariantCulture)].Codigo),
        ];

        Precedencia(cronograma, arestas).Should().BeEmpty();
        arestas.Should().OnlyContain(a => codigosAcusados.Contains(a.AntecessoraCodigo) || codigosAcusados.Contains(a.SucessoraCodigo));
    }
}
