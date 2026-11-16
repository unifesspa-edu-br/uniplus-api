namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoRestaurarConfiguracao;

/// <summary>
/// Os tipos de decisão declarados por fase e etapa ainda não fazem parte da configuração congelada: o grafo
/// reconstruído do envelope não os traz, e a restauração pelo processo repõe a fase e a etapa vivas sem eles.
/// Estes testes caracterizam esse comportamento pelo caminho real da restauração, e não pela reposição direta
/// da entidade.
/// </summary>
public sealed class ProcessoSeletivoRestaurarConfiguracaoTiposDeDecisaoTests
{
    private const string TipoDeClassificacao = TipoMotivoDeDecisaoCodigo.Classificacao;
    private const string TipoDeIsencaoDaTaxa = TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa;

    private static FaseCronograma FaseConformeDeclarando(params string?[] tipos) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("eeee0000-0000-4000-8000-000000000001"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: tipos),
        bancasRequeridas: []).Value!;

    private static FaseCronograma FaseVivaDeclarandoTipos(ProcessoSeletivo processo)
    {
        FaseCronograma viva = processo.CronogramaFases.Single();
        viva.AtualizarDe(FaseConformeDeclarando(TipoDeClassificacao, TipoDeIsencaoDaTaxa), [.. viva.Produtos]);
        return viva;
    }

    private static EtapaProcesso EtapaVivaDeclarandoTipos(ProcessoSeletivo processo)
    {
        EtapaProcesso viva = processo.Etapas.Single();
        viva.DefinirTiposDeDecisao([TipoDeClassificacao, TipoDeIsencaoDaTaxa]).IsSuccess.Should().BeTrue();
        return viva;
    }

    [Fact(DisplayName = "Restauração pelo processo ainda não preserva os tipos de decisão declarados pela fase")]
    public void Restauracao_AindaNaoPreservaOsTiposDeDecisaoDeclaradosDaFase()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        FaseCronograma viva = FaseVivaDeclarandoTipos(processo);
        viva.TiposDeDecisao.Should().Equal(TipoDeClassificacao, TipoDeIsencaoDaTaxa);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(cronogramaFases: [FaseConforme()]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Single().Should().BeSameAs(viva, "a reconciliação por ordem preserva a fase viva");
        viva.TiposDeDecisao.Should().BeEmpty("o grafo congelado ainda não traz os tipos, e a reposição troca a declaração viva pela dele");
    }

    [Fact(DisplayName = "Restauração pelo processo ainda não preserva os tipos de decisão declarados pela etapa")]
    public void Restauracao_AindaNaoPreservaOsTiposDeDecisaoDeclaradosDaEtapa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        EtapaProcesso viva = EtapaVivaDeclarandoTipos(processo);
        GrafoConfiguracao grafo = Grafo(etapas: [EtapaCongeladaDeProvaObjetiva(EtapaOriginal, "Prova Restaurada", ordem: 1, faseCodigo: "RESULTADO_FINAL")]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), grafo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Single().Should().BeSameAs(viva, "a etapa de mesma identidade é reposta no lugar, sem recriação");
        viva.Nome.Should().Be("Prova Restaurada");
        viva.TiposDeDecisao.Should().BeEmpty("o grafo congelado ainda não traz os tipos, e a reposição troca a declaração viva pela dele");
    }

    [Fact(DisplayName = "Restauração pelo processo ainda não preserva a regra de homologação das inscrições declarada pela fase")]
    public void Restauracao_AindaNaoPreservaARegraDeHomologacaoDaFase()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        FaseCronograma viva = processo.CronogramaFases.Single();
        viva.AtualizarDe(FaseConformeDeclarandoRegra(), [.. viva.Produtos]);
        viva.RegraDeHomologacao.Should().NotBeNull();

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), Grafo(cronogramaFases: [FaseConforme()]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        viva.RegraDeHomologacao.Should().BeNull("o grafo congelado ainda não traz a regra, e a reposição troca a declaração viva pela dele");
    }

    private static FaseCronograma FaseConformeDeclarandoRegra() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("eeee0000-0000-4000-8000-000000000001"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null,
            RegraDeHomologacao: RegraDeHomologacaoDasInscricoes.Criar("RESULTADO_CLASSIFICACAO", []).Value!),
        bancasRequeridas: []).Value!;
}
