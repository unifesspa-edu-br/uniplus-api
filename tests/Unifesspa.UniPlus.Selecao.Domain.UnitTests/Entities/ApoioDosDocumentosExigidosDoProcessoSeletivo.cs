namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem comum dos testes dos documentos exigidos do processo seletivo: o processo, as fases,
/// as etapas, as exigências e a gravação das folhas, para que cada assunto parta do mesmo cenário.
/// </summary>
internal static class ApoioDosDocumentosExigidosDoProcessoSeletivo
{
    internal static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Documentos Exigidos", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal static FaseCronograma Fase(int ordem, string codigo, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    internal static FaseCronograma FaseNaOrdem(FaseCronograma fase, int ordem) =>
        Fase(ordem, fase.Codigo, fase.FaseCanonicaOrigemId);

    internal static DocumentoExigido Exigencia(
        Guid exigidoNaFaseId, Aplicabilidade aplicabilidade = Aplicabilidade.Geral, string? consequenciaIndeferimento = null,
        FinalidadeFormulario? finalidade = null) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade,
            obrigatorio: consequenciaIndeferimento is null,
            consequenciaIndeferimento,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
            finalidade: finalidade).Value!;

    internal static DocumentoExigido ExigenciaComIdade(Guid exigidoNaFaseId, IdadeMaximaEmissao idade, FinalidadeFormulario? finalidade = null) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "COMPROVANTE_RESIDENCIA",
            tipoDocumentoNome: "Comprovante de residência",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: idade,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null,
            finalidade: finalidade).Value!;

    internal static EtapaProcesso Etapa(string nome, string faseCodigo, int ordem) =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise documental", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    internal static NoExigencia FolhaDaFase(Guid exigidoNaFaseId, int ordem) =>
        NoExigencia.CriarFolha(Exigencia(exigidoNaFaseId), ordem).Value!;

    internal static ProcessoSeletivo ProcessoComFases(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases(fases, []).IsSuccess.Should().BeTrue();
        return processo;
    }

    internal static void AdicionarEtapas(ProcessoSeletivo processo, params EtapaProcesso[] etapas) =>
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();

    internal static Result DefinirFolhas(ProcessoSeletivo processo, params DocumentoExigido[] exigencias) =>
        processo.DefinirDocumentosExigidos(
            [.. exigencias.Select((exigencia, ordem) => NoExigencia.CriarFolha(exigencia, ordem).Value!)]);

    internal static void ExigirFolhas(ProcessoSeletivo processo, params DocumentoExigido[] exigencias) =>
        DefinirFolhas(processo, exigencias).IsSuccess.Should().BeTrue();

    internal static CondicaoGatilho GatilhoFaixaEtaria() =>
        CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;

    internal static DocumentoExigidoBaseLegal BaseLegalResolvida() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    internal static NoExigenciaBaseLegal BaseLegalDoGrupo(StatusBaseLegal status) =>
        NoExigenciaBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    internal static Guid FaseDoResultado(ProcessoSeletivo processo) => processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;

    /// <summary>O documento geral de teste, exigido na fase, com a obrigatoriedade, a consequência e as bases legais que o cenário pede.</summary>
    internal static DocumentoExigido DocumentoGeral(
        Guid faseId, bool obrigatorio, string? consequencia, IReadOnlyList<DocumentoExigidoBaseLegal>? basesLegais = null, FinalidadeFormulario? finalidade = null) =>
        DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio, consequenciaIndeferimento: consequencia,
            condicoes: [], basesLegais: basesLegais ?? [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: finalidade).Value!;

    /// <summary>O documento obrigatório que só vale quando os gatilhos dados acontecem, exigido na fase, com base legal resolvida.</summary>
    internal static DocumentoExigido DocumentoCondicional(Guid faseId, string codigo, string nome, string categoria, params CondicaoGatilho[] gatilhos) =>
        DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), codigo, nome, categoria, ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: gatilhos, basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    /// <summary>Grupo OU de um documento geral só, com a consequência dada e base legal resolvida, como única exigência do processo.</summary>
    internal static void ExigirGrupoOuDeUmDocumento(ProcessoSeletivo processo, string consequencia)
    {
        NoExigencia folha = NoExigencia.CriarFolha(DocumentoGeral(FaseDoResultado(processo), obrigatorio: false, consequencia: null), 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 1, consequencia, [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [folha]).Value!;

        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();
    }
}
