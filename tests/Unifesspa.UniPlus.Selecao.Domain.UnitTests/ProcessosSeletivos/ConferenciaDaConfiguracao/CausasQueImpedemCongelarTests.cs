namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A completude do processo exige a fase que homologa as inscrições logo depois do que o cronograma exige do processo, e o painel
/// mostra, em cada item da homologação, o mesmo veredicto da conferência.
/// </summary>
public sealed class CausasQueImpedemCongelarTests
{
    private const string ItemDaFase = "cronograma_homologacao_das_inscricoes_exigida";
    private const string ItemDoCiclo = "cronograma_homologacao_das_inscricoes_com_ciclo_recursal";
    private const string ItemDoInep = "cronograma_homologacao_das_inscricoes_com_confirmacao_do_inep";

    private static readonly DateTimeOffset Abertura = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static FaseCronograma FaseDeColeta(RegraDeHomologacaoDasInscricoes? regra) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Abertura, Abertura.AddDays(30), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            [ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Definitivo)],
            EmiteParecerIndividual: false,
            RegraRecurso: null,
            regra is null ? null : HomologacaoDasInscricoesDeTeste.TiposDeDecisao,
            regra),
        bancasRequeridas: []).Value!;

    private static FaseCronograma FaseSemColeta() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static string[] Codigos(ProcessoSeletivo processo) =>
        [.. ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors.Select(static causa => causa.Error.Code)];

    private static IReadOnlyList<FieldError> CausasDaCompletude(ProcessoSeletivo processo)
    {
        ContextoDaConferencia contexto = ContextoDaConferencia.De(processo.Declaracao(), CadastrosDeTeste.Para(processo));

        return [.. CausasQueImpedemCongelar.Assuntos.Single(static assunto => assunto.Nome == NomeDoAssunto.Completude).Causas(contexto)];
    }

    private static string[] VermelhosDaHomologacao(ProcessoSeletivo processo) =>
        [.. ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Where(static item => !item.Ok && item.Codigo.StartsWith("cronograma_homologacao_das_inscricoes", StringComparison.Ordinal))
            .Select(static item => item.Codigo)];

    [Fact(DisplayName = "A falta da fase de coleta é recusada antes da falta da fase que homologa as inscrições")]
    public void Completude_ColetaAntesDaFaseQueHomologa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: FaseSemColeta());

        string[] codigos = Codigos(processo);

        codigos.Should().ContainInOrder(
            "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
            ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes);
    }

    [Fact(DisplayName = "As declarações das fases são recusadas antes da completude, e a completude antes da cobertura dos motivos")]
    public void Assuntos_DeclaracaoDepoisCompletudeDepoisCobertura()
    {
        CenarioDeCongelamentoPorAssunto cenario = CenarioDeCongelamentoPorAssunto.Para("Publicar");
        cenario.Quebrar(new HashSet<string> { NomeDoAssunto.MotivosDeDecisao, NomeDoAssunto.Completude, NomeDoAssunto.Declaracoes });

        string[] comCausa = [.. cenario.CausasPorAssunto().Where(static doAssunto => doAssunto.Causas.Count > 0).Select(static doAssunto => doAssunto.Assunto)];

        comCausa.Should().Equal(NomeDoAssunto.Declaracoes, NomeDoAssunto.Completude, NomeDoAssunto.MotivosDeDecisao);
    }

    [Fact(DisplayName = "Sem a fase que homologa as inscrições, o painel pende o item da fase e a completude traz a causa da origem")]
    public void Paridade_FaseQueHomologaAusente()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: FaseDeColeta(regra: null));

        VermelhosDaHomologacao(processo).Should().Equal(ItemDaFase);
        CausasDaCompletude(processo).Select(static causa => causa.Error.Code).Should().Equal(ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes);
    }

    [Fact(DisplayName = "Na importação externa sem a janela de recurso, o painel pende o item do ciclo e a completude traz a causa do ciclo")]
    public void Paridade_CicloDaImportacaoExterna()
    {
        ProcessoSeletivo processo = ApoioConformidadePublicabilidadeEstrutural.ProcessoConforme();
        processo.DefinirCronogramaFases([ApoioConformidadePublicabilidadeEstrutural.FaseBase()], []).IsSuccess.Should().BeTrue();

        VermelhosDaHomologacao(processo).Should().Equal(ItemDoCiclo);
        CausasDaCompletude(processo).Select(static causa => causa.Error.Code).Should().Equal(ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso);
    }

    [Fact(DisplayName = "Com a nota do ENEM e a regra sem a confirmação do INEP, o painel pende o item do INEP e a completude traz a causa do INEP")]
    public void Paridade_ConfirmacaoDoInep()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(
            fase: FaseDeColeta(RegraDeHomologacaoDasInscricoes.Declarar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, [])));
        ApoioDoDesempatePorAreaEnem.AdicionarEtapasDeNotaDoEnem(processo, 1);

        VermelhosDaHomologacao(processo).Should().Equal(ItemDoInep);
        CausasDaCompletude(processo).Select(static causa => causa.Error.Code).Should().Contain(HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente);
    }

    [Fact(DisplayName = "O processo com a fase que homologa as inscrições tem os três itens da homologação conformes e nenhuma causa de completude")]
    public void Paridade_ProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        VermelhosDaHomologacao(processo).Should().BeEmpty();
        CausasDaCompletude(processo).Should().BeEmpty();
    }
}
