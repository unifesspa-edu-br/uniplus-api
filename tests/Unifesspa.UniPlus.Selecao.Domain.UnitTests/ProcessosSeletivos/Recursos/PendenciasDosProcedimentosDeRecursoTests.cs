namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos.ProcedimentosDeRecursoParaTestes;

/// <summary>A janela de recurso da etapa que declara um tipo de decisão só passa para o congelamento com procedimento para ele.</summary>
public sealed class PendenciasDosProcedimentosDeRecursoTests
{
    private static readonly ReferenciaRegra RegraDoPrazo = ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64));

    private static EtapaProcesso EtapaObjetiva() => EtapaProcesso.Criar(
        new IdentificacaoDaEtapa(
            Nome: "Prova objetiva", Carater: CaraterEtapa.Classificatoria,
            TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
        new DeclaracaoDaAvaliacaoDaEtapa(
            Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva,
            RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcesso EtapaComJanela(ProcedimentosDeRecurso procedimentos, params string[] tiposDeDecisao)
    {
        EtapaProcesso etapa = EtapaObjetiva();
        etapa.DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        etapa.DefinirTiposDeDecisao(tiposDeDecisao).IsSuccess.Should().BeTrue();
        Guid preliminar = etapa.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id;
        etapa.DefinirRecursos([RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado, RegraDoPrazo, new ArgsRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null),
            preliminar, procedimentos).Value!]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static DomainError? Pendencia(EtapaProcesso etapa)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return PendenciasDosProcedimentosDeRecurso.PendenciaDosProcedimentosDeRecurso(EstadoDaConfiguracao.De(processo));
    }

    [Fact(DisplayName = "Janela da etapa que declara o tipo de decisão e não declara procedimento é recusada, citando a etapa e o tipo")]
    public void TipoSemProcedimento_Recusa()
    {
        DomainError? pendencia = Pendencia(EtapaComJanela(ProcedimentosDeRecurso.Nenhum, TipoMotivoDeDecisaoCodigo.Classificacao));

        pendencia!.Code.Should().Be(CodigosDoProcedimentoDeRecurso.DaEtapa.Ausente);
        pendencia.Message.Should().Contain("Prova objetiva").And.Contain("CLASSIFICACAO");
    }

    [Fact(DisplayName = "Janela da etapa com procedimento para o tipo declarado é aceita")]
    public void TipoComProcedimento_Aceita() =>
        Pendencia(EtapaComJanela(ProcedimentosDeRecurso.De([Procedimento()]), TipoMotivoDeDecisaoCodigo.Classificacao)).Should().BeNull();

    [Fact(DisplayName = "Janela da etapa que não declara tipo de decisão não exige procedimento")]
    public void SemTipoDeclarado_Aceita() =>
        Pendencia(EtapaComJanela(ProcedimentosDeRecurso.Nenhum)).Should().BeNull();

    [Theory(DisplayName = "O painel de conformidade espelha o portão: o item do procedimento de recurso é ok só com a janela coberta")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void PainelDeConformidade_EspelhaOPortao(bool declaraProcedimento, bool esperadoOk)
    {
        ProcedimentosDeRecurso procedimentos = declaraProcedimento ? ProcedimentosDeRecurso.De([Procedimento()]) : ProcedimentosDeRecurso.Nenhum;
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas([EtapaComJanela(procedimentos, TipoMotivoDeDecisaoCodigo.Classificacao)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemConformidade> itens = ChecklistDeConformidade.AvaliarConformidade(
            EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo,
            TiposDeAtoUnicosDeTeste.Nenhum, CorretoresDeTeste.SemConsolidacao);

        itens.Single(i => i.Codigo == "cronograma_procedimento_de_recurso").Ok.Should().Be(esperadoOk);
    }
}
