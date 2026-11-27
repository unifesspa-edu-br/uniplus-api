namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Os tipos de decisão que a fase e a etapa declaram, contra o Postgres real: a coluna
/// <c>tipos_de_decisao</c> (<c>text[]</c>) grava, lê, aceita a alteração no lugar da entidade
/// rastreada e assume o default vazio.
/// </summary>
public sealed class TiposDeDecisaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string Heteroidentificacao = "HETEROIDENTIFICACAO";
    private const string AnaliseDocumental = "ANALISE_DOCUMENTAL";
    private const string Classificacao = "CLASSIFICACAO";

    [Fact(DisplayName = "Fase e etapa com tipos declarados gravam e leem de volta em ordem canônica")]
    public async Task TiposDeclarados_GravamELeemDeVolta()
    {
        (Guid faseId, Guid etapaId) = await GravarAsync([Heteroidentificacao, AnaliseDocumental], [Classificacao, AnaliseDocumental]);

        (await RelerFaseAsync(faseId)).TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Heteroidentificacao);
        (await RelerEtapaAsync(etapaId)).TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Classificacao);
    }

    [Fact(DisplayName = "Fase e etapa gravam cada tipo pelo código de contrato, e não pelo nome do enum")]
    public async Task TiposDeclarados_GravamOCodigoDeContratoNaColuna()
    {
        (Guid faseId, Guid etapaId) = await GravarAsync([Heteroidentificacao, AnaliseDocumental], [Classificacao, AnaliseDocumental]);

        (await TextoGravadoDaFaseAsync(faseId)).Should().Be("ANALISE_DOCUMENTAL,HETEROIDENTIFICACAO");
        (await TextoGravadoDaEtapaAsync(etapaId)).Should().Be("ANALISE_DOCUMENTAL,CLASSIFICACAO");
    }

    [Fact(DisplayName = "Fase e etapa sem tipos declarados gravam e leem lista vazia")]
    public async Task SemTipos_GravamELeemListaVazia()
    {
        (Guid faseId, Guid etapaId) = await GravarAsync(fase: [], etapa: []);

        (await RelerFaseAsync(faseId)).TiposDeDecisao.Should().BeEmpty();
        (await RelerEtapaAsync(etapaId)).TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Redefinir o cronograma com outros tipos altera a fase rastreada e a mudança persiste")]
    public async Task RedefinirCronograma_AlteraOsTiposDaFaseRastreada()
    {
        Guid origemId = Guid.CreateVersion7();
        (Guid processoId, Guid faseId, _) = await GravarProcessoAsync(origemId, [AnaliseDocumental], etapa: []);

        await using (UniPlusDbContext db = fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            processo.DefinirCronogramaFases([NovaFase(origemId, [Classificacao, Heteroidentificacao])], [])
                .IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        (await RelerFaseAsync(faseId)).TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.Heteroidentificacao);
    }

    [Fact(DisplayName = "Redefinir o cronograma sem tipos esvazia a fase rastreada e a mudança persiste")]
    public async Task RedefinirCronograma_SemTipos_EsvaziaAFaseRastreada()
    {
        Guid origemId = Guid.CreateVersion7();
        (Guid processoId, Guid faseId, _) = await GravarProcessoAsync(origemId, [AnaliseDocumental], etapa: []);

        await using (UniPlusDbContext db = fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            processo.DefinirCronogramaFases([NovaFase(origemId, [])], []).IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        (await RelerFaseAsync(faseId)).TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Alterar os tipos da etapa rastreada no lugar persiste a mudança")]
    public async Task AlterarTiposDaEtapaRastreada_Persiste()
    {
        (Guid processoId, _, Guid etapaId) = await GravarProcessoAsync(Guid.CreateVersion7(), fase: [], etapa: [AnaliseDocumental]);

        await using (UniPlusDbContext db = fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            processo.Etapas.Single().DefinirTiposDeDecisao([Heteroidentificacao, Classificacao]).IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        (await RelerEtapaAsync(etapaId)).TiposDeDecisao.Should().Equal(TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.Heteroidentificacao);
    }

    [Fact(DisplayName = "Esvaziar os tipos da etapa rastreada persiste a lista vazia")]
    public async Task EsvaziarTiposDaEtapaRastreada_Persiste()
    {
        (Guid processoId, _, Guid etapaId) = await GravarProcessoAsync(Guid.CreateVersion7(), fase: [], etapa: [AnaliseDocumental]);

        await using (UniPlusDbContext db = fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            processo.Etapas.Single().DefinirTiposDeDecisao([]).IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        (await RelerEtapaAsync(etapaId)).TiposDeDecisao.Should().BeEmpty();
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        $"Certame de tipos de decisão {Guid.CreateVersion7()}",
        TiposDeProcessoDeTeste.Sisu,
        OrigemCandidatos.InscricaoPropria,
        Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        IdentificadoresDeTeste.Novo());

    private static FaseCronograma NovaFase(Guid origemId, IReadOnlyList<string?> tipos) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(origemId, "HOMOLOGACAO_INSCRICOES", OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: tipos),
        bancasRequeridas: []).Value!;

    private static EtapaProcesso NovaEtapa(IReadOnlyList<string?> tipos)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Etapa de teste", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirTiposDeDecisao(tipos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.CronogramaFases)
            .Include(p => p.Etapas)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);

    private async Task<(Guid FaseId, Guid EtapaId)> GravarAsync(IReadOnlyList<string?> fase, IReadOnlyList<string?> etapa)
    {
        (_, Guid faseId, Guid etapaId) = await GravarProcessoAsync(Guid.CreateVersion7(), fase, etapa);
        return (faseId, etapaId);
    }

    private async Task<(Guid ProcessoId, Guid FaseId, Guid EtapaId)> GravarProcessoAsync(
        Guid origemId, IReadOnlyList<string?> fase, IReadOnlyList<string?> etapa)
    {
        await using UniPlusDbContext escrita = fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases([NovaFase(origemId, fase)], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([NovaEtapa(etapa)]).IsSuccess.Should().BeTrue();
        escrita.ProcessosSeletivos.Add(processo);
        await escrita.SaveChangesAsync();
        return (processo.Id, processo.CronogramaFases.Single().Id, processo.Etapas.Single().Id);
    }

    private async Task<FaseCronograma> RelerFaseAsync(Guid faseId)
    {
        await using UniPlusDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Set<FaseCronograma>().FirstAsync(f => f.Id == faseId);
    }

    private async Task<EtapaProcesso> RelerEtapaAsync(Guid etapaId)
    {
        await using UniPlusDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Set<EtapaProcesso>().FirstAsync(e => e.Id == etapaId);
    }

    private async Task<string> TextoGravadoDaFaseAsync(Guid faseId)
    {
        await using UniPlusDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Database
            .SqlQuery<string>($"SELECT array_to_string(tipos_de_decisao, ',') AS \"Value\" FROM selecao.fases_cronograma WHERE id = {faseId}")
            .SingleAsync();
    }

    private async Task<string> TextoGravadoDaEtapaAsync(Guid etapaId)
    {
        await using UniPlusDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Database
            .SqlQuery<string>($"SELECT array_to_string(tipos_de_decisao, ',') AS \"Value\" FROM selecao.etapas_processo WHERE id = {etapaId}")
            .SingleAsync();
    }
}
