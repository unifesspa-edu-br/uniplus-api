namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem comum dos testes dos documentos exigidos do processo seletivo: o processo, as fases,
/// as etapas, as exigências e a gravação das regras de documento, para que cada assunto parta do mesmo cenário.
/// </summary>
internal static class ApoioDosDocumentosExigidosDoProcessoSeletivo
{
    internal static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Documentos Exigidos", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal static FaseCronograma Fase(int ordem, string codigo, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>A fase sem data que coleta a inscrição, o pedido de isenção, ou os dois, conforme as marcas.</summary>
    internal static FaseCronograma FaseQueResponde(int ordem, string codigo, bool inscricao, bool isencao) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: inscricao, ColetaSolicitacaoIsencao: isencao)),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    internal static FaseCronograma FaseNaOrdem(FaseCronograma fase, int ordem) =>
        Fase(ordem, fase.Codigo, fase.FaseCanonicaOrigemId);

    internal static DocumentoExigido Exigencia(
        Guid exigidoNaFaseId, Aplicabilidade aplicabilidade = Aplicabilidade.Geral, string? consequenciaDoDescumprimento = null,
        PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao, bool abrePrazoParaReenvio = false) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FormularioDeTeste.CodigoDoPapel(papel)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(aplicabilidade, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(consequenciaDoDescumprimento is null, consequenciaDoDescumprimento, [], abrePrazoParaReenvio))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;

    internal static DocumentoExigido ExigenciaComPrazoEValidade(
        Guid exigidoNaFaseId, PrazoDeEmissao? prazo, ValidadeDoDocumento? validade, PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COMPROVANTE_RESIDENCIA", "Comprovante de residência", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FormularioDeTeste.CodigoDoPapel(papel)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [], false))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, prazo, validade))
            .ComFatos(null)
            .Criar().Value!;

    internal static EtapaProcesso Etapa(string nome, string faseCodigo, int ordem) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise documental", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    internal static RegraDeComprovacao RegraDeDocumentoDaFase(Guid exigidoNaFaseId, int ordem) =>
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(exigidoNaFaseId), ordem).Value!;

    internal static ProcessoSeletivo ProcessoComFases(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases(fases, []).IsSuccess.Should().BeTrue();
        return processo;
    }

    internal static void AdicionarEtapas(ProcessoSeletivo processo, params EtapaProcesso[] etapas) =>
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();

    internal static Result DefinirRegrasDeDocumento(ProcessoSeletivo processo, params DocumentoExigido[] exigencias) =>
        processo.DefinirDocumentosExigidos(
            [.. exigencias.Select((exigencia, ordem) => RegraDeComprovacao.CriarRegraDeDocumento(exigencia, ordem).Value!)]);

    /// <summary>Grava as exigências depois de dar a cada uma o formulário da sua fase, para o teste exercitar só a regra que não é a do formulário.</summary>
    internal static Result DefinirRegrasDeDocumentoNosFormulariosDaFase(ProcessoSeletivo processo, params DocumentoExigido[] exigencias)
    {
        FormularioDeTeste.GarantirOsFormulariosDasExigencias(processo, exigencias);
        return DefinirRegrasDeDocumento(processo, exigencias);
    }

    internal static void ExigirDocumentos(ProcessoSeletivo processo, params DocumentoExigido[] exigencias) =>
        DefinirRegrasDeDocumentoNosFormulariosDaFase(processo, exigencias).IsSuccess.Should().BeTrue();

    internal static CondicaoGatilho GatilhoFaixaEtaria() =>
        CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;

    internal static DocumentoExigidoBaseLegal BaseLegalResolvida() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    internal static BaseLegalDaComprovacao BaseLegalDoGrupo(StatusBaseLegal status) =>
        BaseLegalDaComprovacao.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    internal static Guid FaseDoResultado(ProcessoSeletivo processo) => processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;

    /// <summary>O documento geral de teste, exigido na fase, com a obrigatoriedade, a consequência e as bases legais que o cenário pede.</summary>
    internal static DocumentoExigido DocumentoGeral(
        Guid faseId, bool obrigatorio, string? consequencia, IReadOnlyList<DocumentoExigidoBaseLegal>? basesLegais = null, PapelDoFormularioDeTeste papel = PapelDoFormularioDeTeste.Inscricao) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.CodigoDoPapel(papel)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(obrigatorio, consequencia, basesLegais ?? [], false))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;

    /// <summary>O documento obrigatório que só vale quando os gatilhos dados acontecem, exigido na fase, com base legal resolvida.</summary>
    internal static DocumentoExigido DocumentoCondicional(Guid faseId, string codigo, string nome, string categoria, params CondicaoGatilho[] gatilhos) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), codigo, nome, categoria, ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, gatilhos))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [BaseLegalResolvida()], false))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;

    /// <summary>Grupo com quantidade mínima de um documento, consequência e base legal resolvida.</summary>
    internal static void ExigirGrupoDeQuantidadeMinimaComDocumento(ProcessoSeletivo processo, string consequencia)
    {
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoGeral(FaseDoResultado(processo), obrigatorio: false, consequencia: null), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, consequencia), 0, [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [regraDeDocumento]).Value!;

        processo.DefinirDocumentosExigidosNosFormulariosMinimos([grupo]).IsSuccess.Should().BeTrue();
    }
}
