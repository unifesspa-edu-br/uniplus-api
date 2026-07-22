namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Monta e persiste um <see cref="ProcessoSeletivo"/> minimamente conforme
/// (CA-07: etapas, oferta de atendimento, distribuição de vagas,
/// classificação) mais um <see cref="DocumentoEdital"/> já confirmado — o
/// par exigido por <c>PublicarProcessoSeletivoCommand</c> para publicar com
/// sucesso. Reusado pelos cenários de cascading (V8/V9) e pelos testes de
/// endpoint HTTP — todos exercitam a publicação real, não um agregado de
/// brinquedo (diferente do precedente <c>Edital</c>, cujo <c>Criar/Publicar</c>
/// trivial não existe mais no modelo atual).
/// </summary>
internal static class ProcessoSeletivoPublicavelSeeder
{
    // 64 caracteres hex minúsculos — shape de SHA-256 exigido por
    // HashCanonicalComputer.IsValidHashShape; valor arbitrário (não é
    // recomputado, só precisa satisfazer o formato).
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    public static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> SemearAsync(
        SelecaoDbContext db,
        string nome)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        Result etapasResult = processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, notaMinima: null, ordem: 1),
        ], PrecondicaoIfMatch.Ausente);
        etapasResult.IsSuccess.Should().BeTrue(etapasResult.Error?.Message);

        Result ofertaResult = processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        ofertaResult.IsSuccess.Should().BeTrue(ofertaResult.Error?.Message);

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        Result<ConfiguracaoDistribuicaoVagas> distribuicaoResult = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: regraDistribuicao,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]);
        distribuicaoResult.IsSuccess.Should().BeTrue(distribuicaoResult.Error?.Message);
        Result distribuicaoDefinirResult = processo.DefinirDistribuicaoVagas([distribuicaoResult.Value!], PrecondicaoIfMatch.Ausente);
        distribuicaoDefinirResult.IsSuccess.Should().BeTrue(distribuicaoDefinirResult.Error?.Message);

        ReferenciaRegra regraCalculo = ReferenciaRegra.Criar(
            RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!;
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.Criar(
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!;
        Result<ConfiguracaoClassificacao> classificacaoResult = ConfiguracaoClassificacao.Criar(
            regraCalculo: regraCalculo,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: regraOrdemAlocacao,
            nOpcoesAlocacao: 1,
            regrasEliminacao: []);
        classificacaoResult.IsSuccess.Should().BeTrue(classificacaoResult.Error?.Message);
        Result classificacaoDefinirResult = processo.DefinirClassificacao(classificacaoResult.Value!, PrecondicaoIfMatch.Ausente);
        classificacaoDefinirResult.IsSuccess.Should().BeTrue(classificacaoDefinirResult.Error?.Message);

        FaseCronograma faseConforme = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        Result cronogramaResult = processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente);
        cronogramaResult.IsSuccess.Should().BeTrue(cronogramaResult.Error?.Message);

        await db.ProcessosSeletivos.AddAsync(processo);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue(confirmarResult.Error?.Message);
        await db.DocumentosEdital.AddAsync(documento);

        await db.SaveChangesAsync();

        // A seed bypassa o handler produtivo — drena eventos residuais (não
        // deveria haver nenhum, já que Criar/Definir* não emitem, mas evita
        // vazamento silencioso se essa invariante mudar no futuro).
        processo.DequeueDomainEvents();

        return (processo, documento);
    }
}
