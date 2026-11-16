namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioDoDescarteAposDeslocamentoDeOrdem
{
    internal static readonly string HashFixo = string.Concat(Enumerable.Repeat("de01234567", 7))[..64];
    internal static readonly DateTimeOffset Agora = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// O grafo tal como o codec o entrega ao descarte: fases e produtos reidratados com os
    /// <c>Id</c>s congelados no envelope.
    /// </summary>
    internal static GrafoConfiguracao GrafoCongelado(
        Guid faseInscricaoOrigem,
        Guid fasePreliminarOrigem,
        Guid faseFinalOrigem,
        Guid produtoPreliminarId,
        Guid produtoFinalId) => new(
        etapas: [],
        atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [Distribuicao()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: Classificacao(),
        cronogramaFases:
        [
            FaseReidratada(1, "INSCRICAO", faseInscricaoOrigem, [], null),
            FaseReidratada(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                [ProdutoDaFase.Reidratar(produtoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                "RESULTADO_FINAL"),
            FaseReidratada(3, "RESULTADO_FINAL", faseFinalOrigem,
                [ProdutoDaFase.Reidratar(produtoFinalId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
                null),
        ],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        configuracaoTaxaInscricao: ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
        localidade: LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        numeroDeOpcoesDeCurso: 1);

    internal static ProcessoSeletivo NovoProcesso(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([Distribuicao()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(Classificacao(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }

    internal static ConfiguracaoDistribuicaoVagas Distribuicao() => ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
        new DistribuicaoDeVagasDeclarada
        {
            VagasOfertadasBase = 40,
            PercentualDaReservaLegal = 1m,
            RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            Modalidades = [
            ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
                null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
                quantidadeDeclarada: 40).Value!,
        ]
        },
        new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    internal static ConfiguracaoClassificacao Classificacao() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
        RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
    }).Value!;

    internal static Result<VersaoConfiguracao> Publicar(
        ProcessoSeletivo processo,
        ContextoDeContagemDePrazos? contexto = null) => processo.Publicar(
        DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        "{}"u8.ToArray(),
        "1.1",
        "canonical-json/sha256@v1",
        HashFixo,
        "teste",
        TimeProvider.System,
        contexto ?? ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    internal static ProdutoDaFase ProdutoDe(ProcessoSeletivo processo, string atoCodigo, PapelProdutoFase papel) =>
        processo.CronogramaFases.SelectMany(f => f.Produtos).Single(p => p.AtoCodigo == atoCodigo && p.Papel == papel);

    internal static FaseCronograma Fase(
        int ordem,
        string codigo,
        Guid faseCanonicaOrigemId,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo = null,
        RegraRecursoFase? regraRecurso = null) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                OrigemId: faseCanonicaOrigemId, Codigo: codigo, OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: ordem, Inicio: null, Fim: null, FaseConcluinteCodigo: faseConcluinteCodigo, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: regraRecurso),
            bancasRequeridas: BancasDaFase(regraRecurso)).Value!;

    internal static FaseCronograma FaseReidratada(
        int ordem,
        string codigo,
        Guid faseCanonicaOrigemId,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo,
        RegraRecursoFase? regraRecurso = null) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(),
            new OrigemCanonicaDaFase(
                OrigemId: faseCanonicaOrigemId, Codigo: codigo, OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: ordem, Inicio: null, Fim: null, FaseConcluinteCodigo: faseConcluinteCodigo, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: regraRecurso),
            bancasRequeridas: BancasDaFase(regraRecurso));

    internal static BancaRequerida[] BancasDaFase(RegraRecursoFase? regraRecurso) =>
        regraRecurso is null ? [] : [BancasDeTeste.DeRecursosDaFase()];

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.AtendimentoEspecializado)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Rascunho)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}
