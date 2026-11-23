namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.ModeloDeFase;

/// <summary>
/// A declaração que a fase e a etapa vivas fazem de si: o que a entidade guarda, lido como as regras do
/// processo o leem, e as recusas que só um estado carregado do banco ou reposto por uma janela posterior carrega.
/// </summary>
public sealed class DeclaracaoDasEntidadesTests
{
    private static readonly Guid TipoDaBanca = new("0197d6f0-0000-7000-8000-00000000b4c1");

    private static (string? Campo, string Codigo)[] Causas(IEnumerable<FieldError> recusas) =>
        [.. recusas.Select(static recusa => (recusa.Field, recusa.Error.Code))];

    [Fact(DisplayName = "A fase criada pela fábrica declara os dados que guarda e nenhuma recusa")]
    public void Fase_CriadaPelaFabrica_DeclaraOQueGuardaSemRecusa()
    {
        FaseCronograma fase = new ModeloDeFase("INSCRICAO", 2) { ColetaInscricao = true }.Janela(1, 9).Criar();

        FaseDeclarada declarada = fase.Declaracao();

        declarada.Id.Should().Be(fase.Id);
        declarada.OrigemId.Should().Be(fase.FaseCanonicaOrigemId);
        declarada.Codigo.Should().Be("INSCRICAO");
        declarada.Ordem.Should().Be(2);
        declarada.Janela.Inicio.Should().Be(fase.Inicio);
        declarada.Janela.Fim.Should().Be(fase.Fim);
        declarada.TemUnidadeResponsavel.Should().BeTrue();
        declarada.ColetaInscricao.Should().BeTrue();
        declarada.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase sem unidade responsável declara que não tem unidade")]
    public void Fase_SemUnidade_DeclaraSemUnidade() =>
        new ModeloDeFase("INSCRICAO", 1) { ComUnidade = false }.Criar().Declaracao().TemUnidadeResponsavel.Should().BeFalse();

    [Fact(DisplayName = "A fase que publica o preliminar e recorre dele declara a âncora aceita, o que prova que as publicações foram levadas")]
    public void Fase_ComAncoraNoPreliminar_DeclaraAAncoraAceita()
    {
        ProdutoDaFase preliminar = Preliminar("RESULTADO_PROVA");
        FaseCronograma fase = new ModeloDeFase("RESULTADO", 3)
        {
            Produtos = [preliminar, Definitivo("RESULTADO_PROVA")],
            RegraRecurso = RecursoAncoradoEm(preliminar.Id),
        }.Janela(1, 9).Criar();

        fase.Declaracao().Recusas.Should().BeEmpty();
    }

    // A criação recusa os candidatos atendidos na banca de recursos; o estado violado chega pela reidratação, como o que o banco hidrata.
    private static FaseCronograma FaseReidratadaComCandidatosAtendidosEmBancaDeRecursos()
    {
        BancaRequerida comCandidatosAtendidos = BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.DeRecursos(TipoDaBanca),
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")], BancasDeTeste.Regra());

        return FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            Guid.CreateVersion7(),
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NoEdital, true, false, true, false),
            new DecisoesDeCronogramaDaFase(1, Abertura, Abertura.AddDays(9), UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], false, null),
            [comCandidatosAtendidos]);
    }

    [Fact(DisplayName = "A fase reidratada em estado violado declara a recusa que a criação teria feito")]
    public void Fase_ReidratadaEmEstadoViolado_DeclaraARecusa() =>
        Causas(FaseReidratadaComCandidatosAtendidosEmBancaDeRecursos().Declaracao().Recusas).Should().ContainSingle()
            .Which.Codigo.Should().Be("Banca.CandidatosAtendidosEmBancaDeRecursos");

    [Fact(DisplayName = "O cronograma do processo tem um membro por fase, na ordem em que as fases chegam")]
    public void CronogramaDoProcesso_Do_TemUmMembroPorFaseNaOrdemDeChegada()
    {
        FaseCronograma[] fases = [new ModeloDeFase("SEGUNDA", 2).Criar(), new ModeloDeFase("PRIMEIRA", 1).Criar()];

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(fases);

        cronograma.Fases.Select(static fase => fase.Codigo).Should().Equal("SEGUNDA", "PRIMEIRA");
        cronograma.Fases.Select(static fase => fase.Id).Should().Equal(fases.Select(static fase => fase.Id));
        cronograma.RecusasDasDeclaracoes().Should().BeEmpty();
    }

    private static ModeloDeEtapa ProvaQueEliminaEPontuaComUmItem() => new("Prova objetiva")
    {
        Ordem = 1,
        FaseCodigo = "PROVA",
        Carater = CaraterEtapa.Ambas,
        Peso = 2m,
        NotaMinima = 5m,
        Itens = [new ItemDeAvaliacaoDeclarado(null, "Matemática", 10m)],
    };

    [Fact(DisplayName = "A etapa criada pela fábrica declara os dados que guarda, sem conceito e sem recusa")]
    public void Etapa_CriadaPelaFabrica_DeclaraOQueGuardaSemConceitoESemRecusa()
    {
        EtapaProcesso etapa = ProvaQueEliminaEPontuaComUmItem().Criar();

        EtapaDeclarada declarada = etapa.Declaracao();

        declarada.Id.Should().Be(etapa.Id);
        (declarada.Nome, declarada.Ordem, declarada.FaseCodigo).Should().Be(("Prova objetiva", 1, "PROVA"));
        (declarada.Peso, declarada.NotaMinima).Should().Be((2m, 5m));
        declarada.PontuacoesDosItens.Should().Equal(10m);
        declarada.Conceito.Should().BeNull();
        declarada.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa com janela, bancas e produtos declara tudo isso e nenhuma recusa")]
    public void Etapa_ComFilhas_DeclaraAsFilhasSemRecusa()
    {
        ProdutoDaEtapa preliminar = ModeloDeEtapa.Preliminar("RESULTADO_PROVA");
        EtapaProcesso etapa = new ModeloDeEtapa("Redação") { Produtos = [preliminar, ModeloDeEtapa.Definitivo("RESULTADO_PROVA")], Recursos = [RecursoModelo.AoAto(preliminar.Id)] }
            .Humana().Criar();

        etapa.Declaracao().Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa cuja janela de recurso perdeu o parecer individual declara a recusa da âncora")]
    public void Etapa_QueDesligouOParecerDepoisDeAncorarEmCiencia_DeclaraARecusaDaAncora()
    {
        EtapaProcesso etapa = new ModeloDeEtapa("Prova") { EmiteParecerIndividual = true, Recursos = [RecursoModelo.NaCiencia()] }.Criar();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: false).IsSuccess.Should().BeTrue();

        Causas(etapa.Declaracao().Recusas).Should().Equal(("recursos", "EtapaProcesso.CienciaSemParecerIndividual"));
    }

    [Fact(DisplayName = "As etapas do processo têm um membro por etapa, na ordem em que as etapas chegam")]
    public void EtapasDoProcesso_Das_TemUmMembroPorEtapaNaOrdemDeChegada()
    {
        EtapaProcesso[] etapas = [new ModeloDeEtapa("Redação").Criar(), new ModeloDeEtapa("Prova").Criar()];

        EtapasDoProcesso conjunto = EtapasDoProcesso.Das(etapas);

        conjunto.Etapas.Select(static etapa => etapa.Nome).Should().Equal("Redação", "Prova");
        conjunto.Etapas.Select(static etapa => etapa.Id).Should().Equal(etapas.Select(static etapa => etapa.Id));
        conjunto.RecusasDasDeclaracoes().Should().BeEmpty();
    }
}
