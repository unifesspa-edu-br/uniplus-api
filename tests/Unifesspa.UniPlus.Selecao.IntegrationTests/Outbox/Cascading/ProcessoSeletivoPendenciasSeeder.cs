namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearComCronogramaNaoConformeAsync(
        UniPlusDbContext db, string nome)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder);

        DefinirCronogramaEEtapaDaFase(processo, [faseBuilder(coletaInscricao: false)]);

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
        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out _);

        FaseCronograma faseSemUnidade = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
                UnidadeResponsavel: null),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
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
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "AC", descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla, composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 8).Value!;
        ModalidadeSelecionada cotaSegueCascataForaDoFederal = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "LB_PPI", descricao: "Baixa renda PPI",
            naturezaLegal: NaturezaLegalModalidade.CotaReservada, composicaoVagas: ComposicaoVagasModalidade.DentroDaReservaLegal,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.SegueCascata,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 2).Value!;
        Result<ConfiguracaoDistribuicaoVagas> distribuicaoResult = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 10, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo), Modalidades = [ampla, cotaSegueCascataForaDoFederal] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });
        distribuicaoResult.IsSuccess.Should().BeTrue(distribuicaoResult.Error?.Message);

        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder, distribuicao: [distribuicaoResult.Value!]);

        DefinirCronogramaEEtapaDaFase(processo, [faseBuilder(coletaInscricao: true)]);

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
        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder);
        DefinirCronogramaEEtapaDaFase(processo, [faseBuilder(coletaInscricao: true)]);

        Guid faseId = processo.CronogramaFases.Single().Id;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, TipoDocumentoSeed.Itens[0].Id, "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [baseLegal], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;
        Result exigenciaResult = processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga);
        exigenciaResult.IsSuccess.Should().BeTrue(exigenciaResult.Error?.Message);

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
        ProcessoSeletivo processo = ProcessoBaseConforme(nome, out FaseCronogramaBuilder faseBuilder, definirAtendimentoEspecializado: false);
        DefinirCronogramaEEtapaDaFase(processo, [faseBuilder(coletaInscricao: true)]);

        return documentoConfirmado
            ? await PersistirComDocumentoConfirmadoAsync(db, processo)
            : await PersistirComDocumentoPendenteAsync(db, processo);
    }

    /// <summary>Código do tipo de etapa que a regra exige e o processo base não tem.</summary>
    public const string TipoEtapaNaoOfertada = "ENTREVISTA_NAO_OFERTADA";

    /// <summary>
    /// Semeia uma obrigatoriedade legal universal (Story #853) que exige uma etapa que o
    /// processo base (<see cref="ProcessoSeletivoPublicavelSeeder"/>) não tem — a publicação
    /// recusa por <c>ProcessoSeletivo.ConformidadeLegalInsuficiente</c> com o checklist
    /// ESTRUTURAL 100% verde (CA-05/CA-07: <c>obrigatoriedadesReprovadas</c> aparece,
    /// <c>pendencias</c> não).
    /// </summary>
    /// <remarks>
    /// O tipo de etapa exigido precisa estar vivo no cadastro de Configuração
    /// (<see cref="TiposDeEtapaSeeder"/>): a publicação recusa antes de avaliar quando a
    /// regra referencia código inexistente, e a recusa seria por outro motivo — a regra
    /// ficaria inavaliável em vez de reprovada.
    /// </remarks>
    public static async Task<Guid> SemearObrigatoriedadeNaoAtendidaAsync(UniPlusDbContext db, string regraCodigo)
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            new EtapaObrigatoria(TipoEtapaNaoOfertada),
            new TextoNormativo("Etapa de entrevista obrigatória (teste de integração issue #1096)", "Base legal de teste", null, null),
            new DateOnly(2020, 1, 1),
            null).Value!;

        ObrigatoriedadeLegalRepository repository = new(db, TimeProvider.System);
        await repository.AdicionarAsync(regra, CancellationToken.None);
        await db.SaveChangesAsync();

        return regra.Id;
    }

    private delegate FaseCronograma FaseCronogramaBuilder(bool coletaInscricao);

    /// <summary>
    /// Igual à base, com classificação baseada em ENEM que calcula a média pelos pesos por área
    /// e uma oferta com ou sem grupo de área. Com grupo, o processo é conforme e publica; sem
    /// ele, fica vermelho só "Grupo de área do ENEM congelado em toda oferta", e
    /// <c>Publicar</c> recusa com <c>ProcessoSeletivo.ConformidadeInsuficiente</c>.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearEnemComCalculoLocalAsync(
        UniPlusDbContext db, string nome, bool ofertaComGrupo)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(
            nome, out FaseCronogramaBuilder faseBuilder,
            distribuicao: [DistribuicaoAmplaPadrao(ofertaComGrupo ? ("TECNOLOGICA", "Tecnológica") : null)]);

        Result<ConfiguracaoClassificacao> classificacaoResult = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.FormulaMediaPonderada, "v1", HashFixo),
            RegraArredondamento = ReferenciaRegra.De(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", HashFixo),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        });
        classificacaoResult.IsSuccess.Should().BeTrue(classificacaoResult.Error?.Message);
        Result classificacaoDefinirResult = processo.DefinirClassificacao(classificacaoResult.Value!, PrecondicaoIfMatch.Curinga);
        classificacaoDefinirResult.IsSuccess.Should().BeTrue(classificacaoDefinirResult.Error?.Message);

        // Classificação baseada em ENEM exige ao menos uma edição admitida (issue #1620).
        Result opcoesResult = processo.DefinirOpcoesDeclaradas(
            "EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Curinga);
        opcoesResult.IsSuccess.Should().BeTrue(opcoesResult.Error?.Message);

        DefinirCronogramaEEtapaDaFase(processo, [faseBuilder(coletaInscricao: true)]);

        Result itensResult = processo.DefinirItens([FormularioDeTeste.CampoDaEdicaoDoEnem(0)], PrecondicaoIfMatch.Curinga);
        itensResult.IsSuccess.Should().BeTrue(itensResult.Error?.Message);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoAmplaPadrao((string? Codigo, string? Rotulo)? grupoAreaEnem = null)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "AC", descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla, composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        return ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
    }

    private static void DefinirCronogramaEEtapaDaFase(ProcessoSeletivo processo, IReadOnlyList<FaseCronograma> fases)
    {
        Result cronogramaResult = processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);
        DefinirEtapaDaFase(processo);
    }

    private static void DefinirEtapaDaFase(ProcessoSeletivo processo)
    {
        Result etapasResult = processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ], PrecondicaoIfMatch.Ausente);
        etapasResult.IsSuccess.Should().BeTrue(etapasResult.Error?.Message);
    }

    private static FaseCronograma FaseDeResultadoFinal(bool coletaInscricao) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: coletaInscricao,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static ProcessoSeletivo ProcessoBaseConforme(
        string nome,
        out FaseCronogramaBuilder faseBuilder,
        bool definirAtendimentoEspecializado = true,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas>? distribuicao = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        if (definirAtendimentoEspecializado)
        {
            Result atendimentoResult = processo.DefinirAtendimentoEspecializado(
                AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
            atendimentoResult.IsSuccess.Should().BeTrue(atendimentoResult.Error?.Message);
        }

        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoEfetiva = distribuicao ?? [DistribuicaoAmplaPadrao()];
        Result distribuicaoDefinirResult = processo.DefinirDistribuicaoVagas(distribuicaoEfetiva, FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);
        distribuicaoDefinirResult.IsSuccess.Should().BeTrue(distribuicaoDefinirResult.Error?.Message);

        ReferenciaRegra regraCalculo = ReferenciaRegra.De(
            RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo);
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.De(
            RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo);
        Result<ConfiguracaoClassificacao> classificacaoResult = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = regraCalculo,
            RegraOrdemAlocacao = regraOrdemAlocacao,
        });
        classificacaoResult.IsSuccess.Should().BeTrue(classificacaoResult.Error?.Message);
        Result classificacaoDefinirResult = processo.DefinirClassificacao(classificacaoResult.Value!, PrecondicaoIfMatch.Ausente);
        classificacaoDefinirResult.IsSuccess.Should().BeTrue(classificacaoDefinirResult.Error?.Message);

        faseBuilder = FaseDeResultadoFinal;

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01) — declarada
        // aqui para que cada método desta classe continue isolando SÓ o defeito que se propõe
        // a isolar (issue #1096).
        Result taxaResult = processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente);
        taxaResult.IsSuccess.Should().BeTrue(taxaResult.Error?.Message);
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    private static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> PersistirComDocumentoConfirmadoAsync(
        UniPlusDbContext db, ProcessoSeletivo processo)
    {
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        await db.ProcessosSeletivos.AddAsync(processo);

        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue(confirmarResult.Error?.Message);
        await db.DocumentosAPublicar.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo, documento);
    }

    private static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> PersistirComDocumentoPendenteAsync(
        UniPlusDbContext db, ProcessoSeletivo processo)
    {
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        await db.ProcessosSeletivos.AddAsync(processo);

        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        await db.DocumentosAPublicar.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo, documento);
    }
}
