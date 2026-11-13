namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class DefinirEtapasPrecedenciaDasRecusasTests
{
    private const string Habilitacao = "HABILITACAO";
    private const string Avaliacao = "AVALIACAO";
    private const string FaseInexistente = "FASE_FORA_DO_CRONOGRAMA";

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PSVR 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static TipoEtapaSnapshot Tipo(bool pontua = true, bool notaDoEnem = false) =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), notaDoEnem ? "NOTA_ENEM" : "PROVA_OBJETIVA", "Tipo de teste", admitePontuacao: pontua, admiteEliminacao: true, notaDeOrigemNoEnem: notaDoEnem).Value!;

    private static EtapaProcesso Etapa(string nome, int? ordem, string? faseCodigo = null, Guid? id = null) =>
        EtapaProcesso.Reidratar(id ?? Guid.CreateVersion7(), nome, CaraterEtapa.Classificatoria, Tipo(), 1m, null, ordem, faseCodigo);

    private static EtapaProcesso EtapaSemNota(string nome, int? ordem) =>
        EtapaProcesso.Reidratar(Guid.CreateVersion7(), nome, CaraterEtapa.Eliminatoria, Tipo(pontua: false), null, null, ordem);

    private static EtapaProcesso EtapaDoEnem(int ordem, Guid? id = null, string? faseCodigo = null) =>
        EtapaProcesso.Reidratar(id ?? Guid.CreateVersion7(), "Nota do ENEM", CaraterEtapa.Classificatoria, Tipo(notaDoEnem: true), 1m, null, ordem, faseCodigo);

    private static EtapaProcesso EtapaDoEnemComBanca(int ordem)
    {
        EtapaProcesso etapa = EtapaDoEnem(ordem);
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "BANCA_EXAMINADORA", "Banca de teste")]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static EtapaProcesso EtapaComPreliminaresAbertos(string nome, int ordem, params string[] tiposDeAto)
    {
        EtapaProcesso etapa = Etapa(nome, ordem);
        etapa.DefinirProdutos([.. tiposDeAto.Select(static tipo => ProdutoDaEtapa.Criar(tipo, PapelProdutoFase.Preliminar))]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static EtapaProcesso EtapaComCicloFechado(string nome, int ordem)
    {
        EtapaProcesso etapa = Etapa(nome, ordem);
        etapa.DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static EtapaProcesso EtapaComJanela(string nome, string faseCodigo, string? inicio, string? fim)
    {
        EtapaProcesso etapa = Etapa(nome, null, faseCodigo);
        etapa.DefinirJanelaEParecer(Instante(inicio), Instante(fim), emiteParecerIndividual: false).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static DateTimeOffset? Instante(string? iso) =>
        iso is null ? null : DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static FaseCronograma Fase(int ordem, string codigo, string? inicio = null, string? fim = null) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo,
            inicio is not null && fim is not null ? OrigemDataFase.NoEdital : OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: Instante(inicio), fim: Instante(fim), produtos: [], faseConcluinteCodigo: null,
            emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ConfiguracaoClassificacao Classificacao(bool baseadoEmEnem, params RegraEliminacao[] eliminacoes) =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            eliminacoes,
            baseadoEmEnem,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : [], escalaNotaFinal: 100m).Value!;

    private static RegraEliminacao AusenciaNaEtapa(Guid etapaId) =>
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, 'd'), new ArgsElimAusenciaNaEtapa(etapaId)).Value!;

    private static DocumentoExigido ExigenciaNaEtapa(FaseCronograma fase, EtapaProcesso etapa) =>
        DocumentoExigido.Criar(
            fase.Id,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null,
            exigidoNaEtapaId: etapa.Id,
            finalidade: FinalidadeFormulario.Habilitacao).Value!;

    private static ProcessoSeletivo ComCronograma(ProcessoSeletivo processo, params FaseCronograma[] fases)
    {
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ProcessoSeletivo ComEtapas(ProcessoSeletivo processo, params EtapaProcesso[] etapas)
    {
        processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ProcessoSeletivo ComDesempatePelaNotaDa(ProcessoSeletivo processo, EtapaProcesso etapa)
    {
        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'e'), new ArgsDesempateMaiorNotaEtapa(etapa.Id)).Value!;
        processo.DefinirCriteriosDesempate([criterio], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ProcessoSeletivo ComClassificacao(ProcessoSeletivo processo, ConfiguracaoClassificacao classificacao)
    {
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ProcessoSeletivo ComExigenciasNasEtapas(ProcessoSeletivo processo, FaseCronograma fase, params EtapaProcesso[] etapas)
    {
        IReadOnlyList<NoExigencia> folhas =
            [.. etapas.Select((etapa, indice) => NoExigencia.CriarFolha(ExigenciaNaEtapa(fase, etapa), indice).Value!)];
        processo.DefinirDocumentosExigidos(folhas, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static EtapaProcesso NaFase(EtapaProcesso etapa, string faseCodigo) =>
        EtapaProcesso.Reidratar(etapa.Id, etapa.Nome, etapa.Carater, etapa.TipoEtapa, etapa.Peso, etapa.NotaMinima, etapa.Ordem, faseCodigo);

    private static EtapaProcesso ComoEtapaDoEnem(EtapaProcesso etapa) =>
        EtapaProcesso.Reidratar(etapa.Id, etapa.Nome, etapa.Carater, Tipo(notaDoEnem: true), etapa.Peso, etapa.NotaMinima, etapa.Ordem, etapa.FaseCodigo);

    private static void DeveRecusarCom(Result resultado, string codigo)
    {
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigo);
    }
}
