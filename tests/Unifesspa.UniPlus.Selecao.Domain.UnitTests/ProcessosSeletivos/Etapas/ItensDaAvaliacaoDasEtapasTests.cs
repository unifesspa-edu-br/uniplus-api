namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static ItensDeEtapaDeTeste;
using static ModeloDeEtapa;

public sealed class ItensDaAvaliacaoDasEtapasTests
{
    private static readonly string[] ItensQueDependemDoModo =
    [
        "etapa_com_nota_sem_nota_maxima", "etapa_sem_nota_com_escala", "etapa_com_nota_sem_forma_de_avaliacao",
        "etapa_sem_nota_com_forma_de_avaliacao", "etapa_sem_nota_com_itens_de_avaliacao", "etapa_com_itens_fora_da_nota_maxima",
        "etapa_humana_sem_regra_de_consolidacao", "etapa_humana_sem_passo_do_lancamento", "etapa_humana_sem_banca",
    ];

    private static ModeloDeEtapa HumanaCategorica() => new ModeloDeEtapa("Entrevista").Categorica().Humana() with { Passo = null };

    private static EtapaDeclarada Redacao(ModeloDeEtapa modelo) => (modelo with { Ordem = 1 }).Declarar();

    private static EtapaDeclarada RedacaoSemBancaComItemRecusado() =>
        Redacao(new ModeloDeEtapa("Redação").Humana() with { Bancas = [], Itens = [new ItemDeAvaliacaoDeclarado(null, "Coesão", -1m)] });

    [Fact(DisplayName = "Nota mínima citada acima da nota máxima aceita é reprovada, mesmo com item de avaliação recusado")]
    public void NotaMinimaCitadaAcimaDaNotaMaximaAceita_ContinuaAvaliadaComRecusaEmOutroDado()
    {
        EtapaDeclarada etapa = RedacaoSemBancaComItemRecusado();
        CitacoesDeNota citacoes = new([], [(etapa.Id, 12m)]);

        etapa.Recusado(DadoDaEtapa.ItensDeAvaliacao).Should().BeTrue();
        etapa.Recusado(DadoDaEtapa.NotaMaxima).Should().BeFalse();
        Item("nota_minima_da_regra_acima_da_nota_maxima", citacoes, etapa).Should().Match<ItemConformidade>(
            static item => !item.Ok && item.Mensagem.Contains("(etapas: \"Redação\")", StringComparison.Ordinal));
        Item("etapa_com_itens_fora_da_nota_maxima", citacoes, etapa).Mensagem
            .Should().EndWith("(etapas com declaração recusada: \"Redação\")");
        Item("etapa_com_itens_fora_da_nota_maxima", citacoes, etapa).Ok.Should().BeFalse();
        Item("etapa_humana_sem_banca", citacoes, etapa).Mensagem.Should().EndWith("(etapas: \"Redação\")");
    }

    [Fact(DisplayName = "Carater recusado deixa os itens que dependem do modo vermelhos com o eco de declaração recusada")]
    public void CaraterRecusado_DeixaOsItensDoModoVermelhos()
    {
        EtapaDeclarada etapa = Redacao(new ModeloDeEtapa("Redação") with { Carater = CaraterEtapa.Nenhum });

        Vermelhos(null, etapa).Should().BeEquivalentTo(ItensQueDependemDoModo);
        Item("etapa_humana_sem_banca", etapa).Mensagem.Should().EndWith("(etapas com declaração recusada: \"Redação\")");
        Item("etapa_do_enem_com_escala", etapa).Ok.Should().BeTrue();
        Item("nota_minima_da_regra_acima_da_nota_maxima", etapa).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Etapa de conceito categórico humana com regra e banca fica conforme em todos os itens")]
    public void Categorico_HumanoComRegraEBanca_TodosVerdes()
    {
        EtapaDeclarada etapa = HumanaCategorica().Declarar();

        etapa.Recusas.Should().BeEmpty();
        Vermelhos(null, etapa).Should().BeEmpty();
    }

    [Fact(DisplayName = "Categórico humano sem regra de consolidação reprova só o item da regra")]
    public void Categorico_SemRegra_ReprovaSoARegra()
    {
        EtapaDeclarada etapa = (HumanaCategorica() with { RegraConsolidacao = null }).Declarar();

        etapa.Recusas.Should().BeEmpty();
        Vermelhos(null, etapa).Should().Equal("etapa_humana_sem_regra_de_consolidacao");
    }

    [Fact(DisplayName = "Categórico humano sem banca reprova só o item da banca")]
    public void Categorico_SemBanca_ReprovaSoABanca()
    {
        EtapaDeclarada etapa = (HumanaCategorica() with { Bancas = [] }).Declarar();

        etapa.Recusas.Should().BeEmpty();
        Vermelhos(null, etapa).Should().Equal("etapa_humana_sem_banca");
    }

    [Fact(DisplayName = "Categórico humano sem forma e sem regra, ambas aceitas, reprova só o item da forma")]
    public void Categorico_SemFormaESemRegra_ReprovaSoAForma()
    {
        EtapaDeclarada etapa = (HumanaCategorica() with { Forma = null, RegraConsolidacao = null }).Declarar();

        etapa.Recusas.Should().BeEmpty();
        Vermelhos(null, etapa).Should().Equal("etapa_com_nota_sem_forma_de_avaliacao");
    }

    [Fact(DisplayName = "Categórico com nota máxima é recusado e a nota mínima da regra fica indeterminada nessa etapa")]
    public void Categorico_ComNotaMaxima_RecusaEDeixaANotaMinimaIndeterminada()
    {
        EtapaDeclarada etapa = (HumanaCategorica() with { NotaMaxima = 10m }).Declarar();

        etapa.Recusas.Should().ContainSingle().Which.Field.Should().Be("notaMaxima");
        Item("nota_minima_da_regra_acima_da_nota_maxima", etapa).Should().Match<ItemConformidade>(
            static item => !item.Ok && item.Mensagem.EndsWith("(etapas com declaração recusada: \"Entrevista\")", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Etapa sem nota nem conceito com forma, regra ou itens reprova os itens da etapa sem nota")]
    public void SemNota_ComFormaRegraOuItens_Reprova()
    {
        EtapaDeclarada comForma = (new ModeloDeEtapa("Triagem").SemNota() with { Forma = FormaDeAvaliacao.Objetiva }).Declarar();
        EtapaDeclarada comItens = (new ModeloDeEtapa("Triagem").SemNota() with { Itens = [new ItemDeAvaliacaoDeclarado(null, "Item", 5m)] }).Declarar();

        Vermelhos(null, comForma).Should().Equal("etapa_sem_nota_com_forma_de_avaliacao");
        Vermelhos(null, comItens).Should().Contain("etapa_sem_nota_com_itens_de_avaliacao");
    }

    [Fact(DisplayName = "Nota numérica sem forma de avaliação reprova o item da forma")]
    public void NotaNumerica_SemForma_Reprova()
    {
        Vermelhos(null, (new ModeloDeEtapa("Prova") with { Forma = null }).Declarar()).Should().Equal("etapa_com_nota_sem_forma_de_avaliacao");
    }

    [Fact(DisplayName = "Itens cuja soma difere da nota máxima reprovam e soma igual aprova")]
    public void NotaNumerica_ItensForaDaNotaMaxima_Reprova()
    {
        ItemDeAvaliacaoDeclarado[] somaDez = [new(null, "A", 6m), new(null, "B", 4m)];
        ItemDeAvaliacaoDeclarado[] somaOito = [new(null, "A", 6m), new(null, "B", 2m)];

        Item("etapa_com_itens_fora_da_nota_maxima", (new ModeloDeEtapa("Prova") with { Itens = somaDez }).Declarar()).Ok.Should().BeTrue();
        Item("etapa_com_itens_fora_da_nota_maxima", (new ModeloDeEtapa("Prova") with { Itens = somaOito }).Declarar()).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Humana de nota numérica sem passo e sem escala reprova o passo, e com escala de atendimento não")]
    public void Humana_SemPassoESemEscala_ReprovaOPasso()
    {
        EtapaDeclarada semPasso = (new ModeloDeEtapa("Redação").Humana() with { Passo = null }).Declarar();

        Item("etapa_humana_sem_passo_do_lancamento", semPasso).Ok.Should().BeFalse();
        Item("etapa_humana_sem_passo_do_lancamento", semPasso).Mensagem
            .Should().StartWith("Passo do lançamento de cada etapa de avaliação humana com nota e sem escala de atendimento");
    }

    [Fact(DisplayName = "Os textos dos itens da forma e do passo falam de nota ou conceito")]
    public void Textos_FalamDeNotaOuConceito()
    {
        EtapaDeclarada etapa = new ModeloDeEtapa("Prova").Declarar();

        Item("etapa_com_nota_sem_forma_de_avaliacao", etapa).Mensagem.Should().Be("Forma de avaliação de cada etapa com nota ou conceito que não é do ENEM");
        Item("etapa_sem_nota_com_forma_de_avaliacao", etapa).Mensagem.Should().Be("Forma de avaliação e regra de consolidação só em etapa com nota ou conceito");
    }
}
