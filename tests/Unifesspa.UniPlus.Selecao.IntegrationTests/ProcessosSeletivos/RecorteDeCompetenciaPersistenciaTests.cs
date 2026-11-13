namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O recorte de competência das bancas requeridas por uma fase, contra Postgres real.
/// </summary>
/// <remarks>
/// O teste precisa de banco por duas razões independentes: a reposição do cronograma
/// substitui a banca inteira, e é no <c>SaveChanges</c> que se vê se o recorte da banca que
/// saiu foi removido em cascata em vez de sobrar como linha órfã; e
/// <c>ux_categorias_julgadas_codigo</c> só existe no schema — em memória, a mesma categoria
/// declarada duas vezes na mesma banca passaria sem ninguém perceber.
/// </remarks>
public sealed class RecorteDeCompetenciaPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public RecorteDeCompetenciaPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Duas bancas do mesmo tipo com recortes distintos sobrevivem ao banco como linhas distinguíveis")]
    public async Task DefinirCronograma_DuasBancasDoMesmoTipo_PersisteCadaRecorte()
    {
        Guid tipoUnico = Guid.CreateVersion7();
        Guid processoId = await GravarAsync(
            "PS recorte", Guid.CreateVersion7(), BancaComRecorte(tipoUnico, "RENDA"), BancaComRecorte(tipoUnico, "ETNICO_RACIAL"));

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        IReadOnlyCollection<BancaRequerida> bancas = (await CarregarAsync(db, processoId)).CronogramaFases.Single().BancasRequeridas;

        bancas.Should().OnlyContain(b => b.Codigo == "BANCA_ANALISE_DOCUMENTAL" && b.TipoBancaOrigemId == tipoUnico);
        ConferirQueCadaBancaGuardaOProprioRecorte(bancas);
    }

    private static void ConferirQueCadaBancaGuardaOProprioRecorte(IReadOnlyCollection<BancaRequerida> bancas)
    {
        bancas.Should().HaveCount(2);
        bancas.SelectMany(b => b.RecorteDeCompetencia).Select(c => c.Codigo)
            .Should().BeEquivalentTo(["RENDA", "ETNICO_RACIAL"],
                "cada banca guarda o próprio recorte — é ele que separa uma da outra no snapshot publicado");
        bancas.SelectMany(b => b.RecorteDeCompetencia)
            .Should().OnlyContain(c => c.BancaRequeridaId != Guid.Empty);
    }

    [Fact(DisplayName = "Regravar o cronograma repõe o recorte sem deixar categoria órfã da banca que saiu")]
    public async Task RedefinirCronograma_OutroRecorte_SubstituiSemDeixarOrfa()
    {
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        Guid tipoBanca = Guid.CreateVersion7();
        Guid processoId = await GravarAsync("PS recorte reposto", faseCanonicaOrigemId, BancaComRecorte(tipoBanca, "RENDA"));

        await RedefinirAsync(processoId, faseCanonicaOrigemId, BancaComRecorte(tipoBanca, "ESCOLARIDADE"));

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.CronogramaFases.Single().BancasRequeridas.Single()
            .RecorteDeCompetencia.Should().ContainSingle()
            .Which.Codigo.Should().Be("ESCOLARIDADE");
        await ConferirQueNenhumaCategoriaFicouOrfaAsync(db);
    }

    [Fact(DisplayName = "O índice único recusa a mesma categoria duas vezes na mesma banca, via SQL cru")]
    public async Task IndiceUnico_MesmaCategoriaDuasVezesNaMesmaBanca_Recusa()
    {
        Guid processoId = await GravarAsync("PS recorte unico", Guid.CreateVersion7(), BancaComRecorte(Guid.CreateVersion7(), "RENDA"));

        await using SelecaoDbContext direto = _fixture.CreateDbContext();
        Guid bancaId = (await CarregarAsync(direto, processoId)).CronogramaFases.Single().BancasRequeridas.Single().Id;

        // A recusa no agregado é check-then-act não-atômico: duas requisições concorrentes
        // sobre a mesma banca passariam as duas pela leitura. A constraint é a defesa que
        // não depende de ordem.
        Func<Task> inserirDuplicata = async () => await direto.Database.ExecuteSqlAsync(
            $"INSERT INTO selecao.categorias_julgadas (id, banca_requerida_id, categoria_documento_origem_id, codigo, created_at) VALUES ({Guid.CreateVersion7()}, {bancaId}, {Guid.CreateVersion7()}, {"RENDA"}, {DateTimeOffset.UtcNow})");

        Npgsql.PostgresException violacao = (await inserirDuplicata.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        violacao.SqlState.Should().Be("23505");
        violacao.ConstraintName.Should().Be("ux_categorias_julgadas_codigo");
    }

    // A banca antiga foi removida com a reposição; o recorte dela não pode
    // sobreviver ao pai, ou o certame passaria a declarar competência de uma banca
    // que ninguém mais requer.
    private static async Task ConferirQueNenhumaCategoriaFicouOrfaAsync(SelecaoDbContext db)
    {
        List<Guid> bancasVivas = await db.BancasRequeridas.Select(b => b.Id).ToListAsync();
        (await db.Set<CategoriaJulgada>().CountAsync(c => !bancasVivas.Contains(c.BancaRequeridaId)))
            .Should().Be(0, "nenhuma categoria julgada sobrevive à banca que a declarou");
    }

    private static BancaRequerida BancaComRecorte(Guid tipoBanca, string categoriaCodigo) =>
        BancaRequerida.Criar(
            tipoBanca,
            "BANCA_ANALISE_DOCUMENTAL",
            "Banca de teste",
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), categoriaCodigo)]);

    private async Task<Guid> GravarAsync(string nome, Guid faseCanonicaOrigemId, params BancaRequerida[] bancas)
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso($"{nome} {Guid.CreateVersion7()}");
        processo.DefinirCronogramaFases([Fase(faseCanonicaOrigemId, bancas)], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();
        return processo.Id;
    }

    private async Task RedefinirAsync(Guid processoId, Guid faseCanonicaOrigemId, params BancaRequerida[] bancas)
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);
        Result redefinicao = processo.DefinirCronogramaFases([Fase(faseCanonicaOrigemId, bancas)], [], PrecondicaoIfMatch.Curinga);
        redefinicao.IsSuccess.Should().BeTrue(redefinicao.Error?.Message);
        await db.SaveChangesAsync();
    }

    private static ProcessoSeletivo NovoProcesso(string nome) => ProcessoSeletivo.Criar(
        nome, TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FaseCronograma Fase(Guid faseCanonicaOrigemId, IReadOnlyList<BancaRequerida> bancas) =>
        FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: faseCanonicaOrigemId,
            codigo: "ANALISE_DOCUMENTAL",
            origemData: OrigemDataFase.NaExecucao,
            agrupaEtapas: false,
            permiteComplementacao: false,
            coletaInscricao: false,
            coletaSolicitacaoIsencao: false,
            inicio: null,
            fim: null,
            produtos: [],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas: bancas,
            regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}
