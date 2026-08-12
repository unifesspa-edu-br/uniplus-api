namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Variantes de <see cref="ProcessoSeletivoPublicavelSeeder"/> — cada método isola UM defeito
/// estrutural específico (issue #1096), para provar via HTTP que
/// <c>Extensions["pendencias"]</c> aparece em toda recusa 422 com checklist vermelho, não só
/// quando o erro é <c>ProcessoSeletivo.ConformidadeInsuficiente</c>.
/// </summary>
internal static class ProcessoSeletivoPendenciasSeeder
{
    // 64 caracteres hex minúsculos — shape de SHA-256 exigido por
    // HashCanonicalComputer.IsValidHashShape; valor arbitrário (não é recomputado).
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    /// <summary>
    /// Igual a <see cref="ProcessoSeletivoPublicavelSeeder.SemearAsync"/>, exceto que a única
    /// fase do cronograma NÃO coleta inscrição — com <c>OrigemCandidatos.InscricaoPropria</c>,
    /// isso deixa vermelho só "Cronograma: inscrição própria tem fase que coleta inscrição" e
    /// <c>Publicar</c> recusa com <c>ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta</c> — o
    /// cenário Gherkin da issue #1096.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> SemearComCronogramaNaoConformeAsync(
        SelecaoDbContext db, string nome)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder);

        Result cronogramaResult = processo.DefinirCronogramaFases(
            [faseBuilder(coletaInscricao: false)], [], PrecondicaoIfMatch.Ausente);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    /// <summary>
    /// Igual à base, exceto que a distribuição de vagas usa a regra Institucional (fora do
    /// regime da Lei 12.711) com uma modalidade <c>SegueCascata</c> — vermelho em "Cascata de
    /// remanejamento" e no detalhamento "Cascata: modalidade SegueCascata usa a regra de
    /// distribuição federal"; <c>Publicar</c> recusa com
    /// <c>ProcessoSeletivo.CascataForaDoRegimeFederal</c>.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> SemearComCascataNaoConformeAsync(
        SelecaoDbContext db, string nome)
    {
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "AC", descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla, composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: [], acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 8).Value!;
        ModalidadeSelecionada cotaSegueCascataForaDoFederal = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "LB_PPI", descricao: "Baixa renda PPI",
            naturezaLegal: NaturezaLegalModalidade.CotaReservada, composicaoVagas: ComposicaoVagasModalidade.DentroDoVr,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.SegueCascata,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: [], acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 2).Value!;
        Result<ConfiguracaoDistribuicaoVagas> distribuicaoResult = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(), voBase: 10, pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            regraAjuste: null, referenciaDemografica: null, modalidades: [ampla, cotaSegueCascataForaDoFederal]);
        distribuicaoResult.IsSuccess.Should().BeTrue(distribuicaoResult.Error?.Message);

        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder, distribuicao: [distribuicaoResult.Value!]);

        Result cronogramaResult = processo.DefinirCronogramaFases(
            [faseBuilder(coletaInscricao: true)], [], PrecondicaoIfMatch.Ausente);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    /// <summary>
    /// Igual à base, mais uma exigência documental CONDICIONAL sem nenhuma condição de
    /// gatilho — vermelho em "Exigência documental: sem CONDICIONAL vazia que determina
    /// resultado"; <c>Publicar</c> recusa com
    /// <c>DocumentoExigido.CondicionalVaziaDeterminaResultado</c>.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> SemearComPreCanonicalizacaoNaoConformeAsync(
        SelecaoDbContext db, string nome)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder);
        Result cronogramaResult = processo.DefinirCronogramaFases(
            [faseBuilder(coletaInscricao: true)], [], PrecondicaoIfMatch.Ausente);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);

        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [baseLegal], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        Result exigenciaResult = processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga);
        exigenciaResult.IsSuccess.Should().BeTrue(exigenciaResult.Error?.Message);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    /// <summary>
    /// Igual à base, exceto que <c>DefinirOfertaAtendimento</c> nunca é chamado — vermelho em
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
    public static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> SemearComCadastroProprioNaoConformeAsync(
        SelecaoDbContext db, string nome, bool documentoConfirmado = true)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder, definirOfertaAtendimento: false);
        Result cronogramaResult = processo.DefinirCronogramaFases(
            [faseBuilder(coletaInscricao: true)], [], PrecondicaoIfMatch.Ausente);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);

        return documentoConfirmado
            ? await PersistirComDocumentoConfirmadoAsync(db, processo)
            : await PersistirComDocumentoPendenteAsync(db, processo);
    }

    /// <summary>
    /// Semeia uma obrigatoriedade legal universal (Story #853) que exige uma etapa que o
    /// processo base (<see cref="ProcessoSeletivoPublicavelSeeder"/>) não tem — a publicação
    /// recusa por <c>ProcessoSeletivo.ConformidadeLegalInsuficiente</c> com o checklist
    /// ESTRUTURAL 100% verde (CA-05/CA-07: <c>obrigatoriedadesReprovadas</c> aparece,
    /// <c>pendencias</c> não).
    /// </summary>
    public static async Task<Guid> SemearObrigatoriedadeNaoAtendidaAsync(SelecaoDbContext db, string regraCodigo)
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: new EtapaObrigatoria("ENTREVISTA_NAO_OFERTADA"),
            descricaoHumana: "Etapa de entrevista obrigatória (teste de integração issue #1096)",
            baseLegal: "Base legal de teste",
            vigenciaInicio: new DateOnly(2020, 1, 1)).Value!;

        ObrigatoriedadeLegalRepository repository = new(db, TimeProvider.System);
        await repository.AdicionarAsync(regra, CancellationToken.None);
        await db.SaveChangesAsync();

        return regra.Id;
    }

    private delegate FaseCronograma FaseCronogramaBuilder(bool coletaInscricao);

    private static ConfiguracaoDistribuicaoVagas DistribuicaoAmplaPadrao()
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "AC", descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla, composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: [], acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(), voBase: 40, pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            regraAjuste: null, referenciaDemografica: null, modalidades: [modalidade]).Value!;
    }

    private static ProcessoSeletivo ProcessoBaseConforme(
        string nome,
        out FaseCronogramaBuilder faseBuilder,
        bool definirOfertaAtendimento = true,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas>? distribuicao = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

        Result etapasResult = processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, notaMinima: null, ordem: 1),
        ], PrecondicaoIfMatch.Ausente);
        etapasResult.IsSuccess.Should().BeTrue(etapasResult.Error?.Message);

        if (definirOfertaAtendimento)
        {
            Result ofertaResult = processo.DefinirOfertaAtendimento(
                OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
            ofertaResult.IsSuccess.Should().BeTrue(ofertaResult.Error?.Message);
        }

        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoEfetiva = distribuicao ?? [DistribuicaoAmplaPadrao()];
        Result distribuicaoDefinirResult = processo.DefinirDistribuicaoVagas(distribuicaoEfetiva, PrecondicaoIfMatch.Ausente);
        distribuicaoDefinirResult.IsSuccess.Should().BeTrue(distribuicaoDefinirResult.Error?.Message);

        ReferenciaRegra regraCalculo = ReferenciaRegra.Criar(
            RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!;
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.Criar(
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!;
        Result<ConfiguracaoClassificacao> classificacaoResult = ConfiguracaoClassificacao.Criar(
            regraCalculo: regraCalculo, regraArredondamento: null, casasArredondamento: null,
            regraOrdemAlocacao: regraOrdemAlocacao, nOpcoesAlocacao: 1, regrasEliminacao: [], baseadoEmEnem: false);
        classificacaoResult.IsSuccess.Should().BeTrue(classificacaoResult.Error?.Message);
        Result classificacaoDefinirResult = processo.DefinirClassificacao(classificacaoResult.Value!, PrecondicaoIfMatch.Ausente);
        classificacaoDefinirResult.IsSuccess.Should().BeTrue(classificacaoDefinirResult.Error?.Message);

        faseBuilder = coletaInscricao => FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria,
            agrupaEtapas: true, permiteComplementacao: false, produzResultado: true, resultadoDefinitivo: true,
            coletaInscricao: coletaInscricao,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null).Value!;

        return processo;
    }

    private static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> PersistirComDocumentoConfirmadoAsync(
        SelecaoDbContext db, ProcessoSeletivo processo)
    {
        await db.ProcessosSeletivos.AddAsync(processo);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue(confirmarResult.Error?.Message);
        await db.DocumentosEdital.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo, documento);
    }

    private static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> PersistirComDocumentoPendenteAsync(
        SelecaoDbContext db, ProcessoSeletivo processo)
    {
        await db.ProcessosSeletivos.AddAsync(processo);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        await db.DocumentosEdital.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo, documento);
    }
}
