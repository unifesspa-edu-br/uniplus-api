namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoProcessoConformeSemeado;

/// <summary>
/// Variantes de <see cref="ProcessoSeletivoPublicavelSeeder"/> — cada método isola UM defeito
/// estrutural específico (issue #1096), para provar via HTTP que
/// <c>Extensions["pendencias"]</c> aparece em toda recusa 422 com checklist vermelho, não só
/// quando o erro é <c>ProcessoSeletivo.ConformidadeInsuficiente</c>.
/// </summary>
internal static class ProcessoSeletivoPendenciasSeeder
{
    /// <summary>
    /// Igual a <see cref="ProcessoSeletivoPublicavelSeeder.SemearAsync"/>, exceto que a única
    /// fase do cronograma NÃO coleta inscrição — com <c>OrigemCandidatos.InscricaoPropria</c>,
    /// isso deixa vermelho só "Cronograma: inscrição própria tem fase que coleta inscrição" e
    /// <c>Publicar</c> recusa com <c>ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta</c> — o
    /// cenário Gherkin da issue #1096.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearComCronogramaNaoConformeAsync(
        UniPlusDbContext db, string nome)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(db, nome);

        DefinirCronogramaEEtapaDaFase(processo, [FaseDeResultadoFinal(coletaInscricao: false)]);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    /// <summary>
    /// Igual à base, exceto que a fase do cronograma está sem unidade responsável — vermelho só
    /// em "Cronograma: toda fase tem a unidade responsável definida"; <c>Publicar</c> recusa com
    /// <c>ProcessoSeletivo.FaseSemUnidadeResponsavel</c>.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearComFaseSemUnidadeResponsavelAsync(
        UniPlusDbContext db, string nome)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(db, nome);

        FaseCronograma faseSemUnidade = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
                ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                UnidadeResponsavel: null),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;
        DefinirCronogramaEEtapaDaFase(processo, [faseSemUnidade]);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    /// <summary>
    /// Igual à base, exceto que a distribuição de vagas usa a regra Institucional (fora do
    /// regime da Lei 12.711) com uma modalidade <c>SegueCascata</c> — vermelho em "Cascata de
    /// remanejamento" e no detalhamento "Cascata: modalidade SegueCascata usa a regra de
    /// distribuição federal"; <c>Publicar</c> recusa com
    /// <c>ProcessoSeletivo.CascataForaDoRegimeFederal</c>.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearComCascataNaoConformeAsync(
        UniPlusDbContext db, string nome)
    {
        ConfiguracaoDistribuicaoVagas distribuicao = DistribuicaoComCascataForaDoRegimeFederal(db);

        ProcessoSeletivo processo = ProcessoBaseConforme(db, nome, distribuicao: [distribuicao]);

        DefinirCronogramaEEtapaDaFase(processo, [FaseDeResultadoFinal(coletaInscricao: true)]);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    /// <summary>
    /// Igual à base, mais uma exigência documental CONDICIONAL sem nenhuma condição de
    /// gatilho — vermelho em "Exigência documental: sem CONDICIONAL vazia que determina
    /// resultado"; <c>Publicar</c> recusa com
    /// <c>DocumentoExigido.CondicionalVaziaDeterminaResultado</c>.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearComPreCanonicalizacaoNaoConformeAsync(
        UniPlusDbContext db, string nome)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(db, nome);
        DefinirCronogramaEEtapaDaFase(processo, [FaseDeResultadoFinal(coletaInscricao: true)]);

        ExigirCertidaoComCondicionalVazia(processo);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    /// <summary>
    /// Igual à base, exceto que <c>DefinirAtendimentoEspecializado</c> nunca é chamado — vermelho em
    /// "Atendimento especializado"; <c>Publicar</c> recusa com o gate agregador
    /// <c>ProcessoSeletivo.ConformidadeInsuficiente</c> (já funcionava antes da issue #1096 —
    /// serve de regressão).
    /// </summary>
    /// <param name="documentoConfirmado">
    /// Quando <see langword="false"/>, o documento fica pendente (nunca confirmado): a
    /// publicação recusa por <c>ProcessoSeletivo.DocumentoNaoConfirmado</c> — um erro
    /// completamente alheio à conformidade estrutural — antes de alcançar o gate de
    /// conformidade. Usado para provar que <c>pendencias</c> aparece mesmo quando o 422 não
    /// tem nada a ver com o item vermelho (CA-06).
    /// </param>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearComCadastroProprioNaoConformeAsync(
        UniPlusDbContext db, string nome, bool documentoConfirmado = true)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(db, nome, definirAtendimentoEspecializado: false);
        DefinirCronogramaEEtapaDaFase(processo, [FaseDeResultadoFinal(coletaInscricao: true)]);

        return documentoConfirmado
            ? await PersistirComDocumentoConfirmadoAsync(db, processo)
            : await PersistirComDocumentoPendenteAsync(db, processo);
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoComCascataForaDoRegimeFederal(UniPlusDbContext db)
    {
        ModalidadeSelecionada ampla = AmplaDaOfertaNaoFederal();
        ModalidadeSelecionada cotaSegueCascataForaDoFederal = CotaComCascataForaDoRegimeFederal();
        Result<ConfiguracaoDistribuicaoVagas> distribuicaoResult = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 10, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = RegraDoCatalogo(db, RegraDeDistribuicaoDeVagas.Institucional.Codigo()), Modalidades = [ampla, cotaSegueCascataForaDoFederal] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });
        distribuicaoResult.IsSuccess.Should().BeTrue(distribuicaoResult.Error?.Message);
        return distribuicaoResult.Value!;
    }

    private static ModalidadeSelecionada AmplaDaOfertaNaoFederal()
    {
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Descricao = "Ampla concorrência", RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 8).Value!;
        return ampla;
    }

    private static ModalidadeSelecionada CotaComCascataForaDoRegimeFederal()
    {
        ModalidadeSelecionada cotaSegueCascataForaDoFederal = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal) with { Descricao = "Baixa renda PPI", RegraRemanejamento = RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento, BaseLegal = "Res. Unifesspa 532/2021" }, 2).Value!;
        return cotaSegueCascataForaDoFederal;
    }

    private static void ExigirCertidaoComCondicionalVazia(ProcessoSeletivo processo)
    {
        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_certidao_reservista").Value!,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(TipoDocumentoSeed.Itens[0].Id, "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(faseId, null, FinalidadeFormulario.Inscricao),
                new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, []),
                new IndeferimentoDoDocumento(true, null, [baseLegal]),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
                null)).Value!;
        Result exigenciaResult = processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]);
        exigenciaResult.IsSuccess.Should().BeTrue(exigenciaResult.Error?.Message);
    }
}
