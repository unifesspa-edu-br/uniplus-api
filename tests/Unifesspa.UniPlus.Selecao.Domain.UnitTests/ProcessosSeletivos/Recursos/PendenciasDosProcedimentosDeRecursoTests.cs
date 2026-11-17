namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
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

    internal static EtapaProcesso EtapaComJanela(ProcedimentosDeRecurso procedimentos, params string[] tiposDeDecisao)
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
            AncoraDoRecurso.AtoPublicado, RegraDoPrazo, new ParametrosRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null),
            preliminar, procedimentos).Value!]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static IReadOnlyList<FieldError> Causas(EtapaProcesso etapa)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();
        return PendenciasDosProcedimentosDeRecurso.CausasDosProcedimentosDeRecurso(processo.Declaracao());
    }

    private static DomainError? Pendencia(EtapaProcesso etapa) => Causas(etapa) is { Count: > 0 } causas ? causas[0].Error : null;

    [Fact(DisplayName = "Janela da etapa que declara o tipo de decisão e não declara procedimento é recusada, citando a etapa e o tipo")]
    public void TipoSemProcedimento_Recusa()
    {
        DomainError? pendencia = Pendencia(EtapaComJanela(ProcedimentosDeRecurso.Nenhum, TipoMotivoDeDecisaoCodigo.Classificacao));

        pendencia!.Code.Should().Be(CodigosDoProcedimentoDeRecurso.DaEtapa.Ausente);
        pendencia.Message.Should().Contain("Prova objetiva").And.Contain("CLASSIFICACAO");
    }

    [Fact(DisplayName = "Dois tipos de decisão sem procedimento na mesma janela dão duas causas, na ordem dos tipos")]
    public void DoisTiposSemProcedimento_DuasCausas()
    {
        IReadOnlyList<FieldError> causas = Causas(EtapaComJanela(
            ProcedimentosDeRecurso.Nenhum, TipoMotivoDeDecisaoCodigo.Classificacao, TipoMotivoDeDecisaoCodigo.AnaliseDocumental));

        causas.Select(static causa => causa.Error.Code).Should().Equal(
            CodigosDoProcedimentoDeRecurso.DaEtapa.Ausente, CodigosDoProcedimentoDeRecurso.DaEtapa.Ausente);
        causas[0].Error.Message.Should().Contain("ANALISE_DOCUMENTAL");
        causas[1].Error.Message.Should().Contain("CLASSIFICACAO");
    }

    [Fact(DisplayName = "Janela da etapa com procedimento para o tipo declarado é aceita")]
    public void TipoComProcedimento_Aceita() =>
        Pendencia(EtapaComJanela(ProcedimentosDeRecurso.De([Procedimento()]), TipoMotivoDeDecisaoCodigo.Classificacao)).Should().BeNull();

    [Fact(DisplayName = "Janela da etapa que não declara tipo de decisão não exige procedimento")]
    public void SemTipoDeclarado_Aceita() =>
        Pendencia(EtapaComJanela(ProcedimentosDeRecurso.Nenhum)).Should().BeNull();

    [Fact(DisplayName = "A conferência localiza a recusa do procedimento de recurso no recurso da etapa")]
    public void Conferencia_RecusaDoProcedimento_ApontaORecursoDaEtapa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas([EtapaComJanela(ProcedimentosDeRecurso.Nenhum, TipoMotivoDeDecisaoCodigo.Classificacao)]).IsSuccess.Should().BeTrue();

        FieldError causa = ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Padrao).Errors
            .Single(static causa => causa.Error.Code == CodigosDoProcedimentoDeRecurso.DaEtapa.Ausente);

        causa.Field.Should().Be("etapas[0].recursos");
    }

    [Theory(DisplayName = "O painel de conformidade espelha a conferência: o item do procedimento de recurso é ok só com a janela coberta, e a conferência recusa só sem ela")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void PainelDeConformidade_EspelhaAConferencia(bool declaraProcedimento, bool esperadoOk)
    {
        ProcedimentosDeRecurso procedimentos = declaraProcedimento ? ProcedimentosDeRecurso.De([Procedimento()]) : ProcedimentosDeRecurso.Nenhum;
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirEtapas([EtapaComJanela(procedimentos, TipoMotivoDeDecisaoCodigo.Classificacao)]).IsSuccess.Should().BeTrue();

        IReadOnlyList<ItemConformidade> itens = ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Padrao);

        itens.Single(i => i.Codigo == "cronograma_procedimento_de_recurso").Ok.Should().Be(esperadoOk);
        ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Padrao).Errors
            .Any(static causa => causa.Error.Code == CodigosDoProcedimentoDeRecurso.DaEtapa.Ausente).Should().Be(!esperadoOk);
    }
}
