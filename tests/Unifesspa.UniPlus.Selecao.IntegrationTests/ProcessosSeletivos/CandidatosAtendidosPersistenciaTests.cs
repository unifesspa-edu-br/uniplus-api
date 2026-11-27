namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os candidatos atendidos das bancas requeridas por uma fase, contra Postgres real.
/// </summary>
/// <remarks>
/// O teste precisa de banco por duas razões independentes: a reposição do cronograma
/// substitui a banca inteira, e é no <c>SaveChanges</c> que se vê se os candidatos atendidos da banca que
/// saiu foram removidos em cascata em vez de sobrarem como linhas órfãs; e
/// <c>ux_categorias_julgadas_codigo</c> só existe no schema — em memória, a mesma categoria
/// declarada duas vezes na mesma banca passaria sem ninguém perceber.
/// </remarks>
public sealed class CandidatosAtendidosPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public CandidatosAtendidosPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Duas bancas do mesmo tipo com candidatos atendidos distintos sobrevivem ao banco como linhas distinguíveis")]
    public async Task DefinirCronograma_DuasBancasDoMesmoTipo_PersisteOsCandidatosAtendidosDeCada()
    {
        Guid tipoUnico = Guid.CreateVersion7();
        Guid processoId = await GravarAsync(
            "PS candidatos atendidos", Guid.CreateVersion7(), BancaComCandidatosAtendidos(tipoUnico, "RENDA"), BancaComCandidatosAtendidos(tipoUnico, "ETNICO_RACIAL"));

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        IReadOnlyCollection<BancaRequerida> bancas = (await CarregarAsync(db, processoId)).CronogramaFases.Single().BancasRequeridas;

        bancas.Should().OnlyContain(b => b.Tipo.Codigo == TipoDeBanca.AnaliseDocumental && b.Tipo.OrigemId == tipoUnico);
        ConferirQueCadaBancaGuardaOsPropriosCandidatosAtendidos(bancas);
    }

    private static void ConferirQueCadaBancaGuardaOsPropriosCandidatosAtendidos(IReadOnlyCollection<BancaRequerida> bancas)
    {
        bancas.Should().HaveCount(2);
        bancas.SelectMany(b => b.CandidatosAtendidos).Select(c => c.Codigo)
            .Should().BeEquivalentTo(["RENDA", "ETNICO_RACIAL"],
                "cada banca guarda os próprios candidatos atendidos — são eles que separam uma da outra no snapshot publicado");
        bancas.SelectMany(b => b.CandidatosAtendidos)
            .Should().OnlyContain(c => c.BancaRequeridaId != Guid.Empty);
    }

    [Fact(DisplayName = "Regravar o cronograma repõe os candidatos atendidos sem deixar categoria órfã da banca que saiu")]
    public async Task RedefinirCronograma_OutrosCandidatosAtendidos_SubstituiSemDeixarOrfa()
    {
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        Guid tipoBanca = Guid.CreateVersion7();
        Guid processoId = await GravarAsync("PS candidatos atendidos reposto", faseCanonicaOrigemId, BancaComCandidatosAtendidos(tipoBanca, "RENDA"));

        await RedefinirAsync(processoId, faseCanonicaOrigemId, BancaComCandidatosAtendidos(tipoBanca, "ESCOLARIDADE"));

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.CronogramaFases.Single().BancasRequeridas.Single()
            .CandidatosAtendidos.Should().ContainSingle()
            .Which.Codigo.Should().Be("ESCOLARIDADE");
        await ConferirQueNenhumaCategoriaFicouOrfaAsync(db);
    }

    [Fact(DisplayName = "O índice único recusa a mesma categoria duas vezes na mesma banca, via SQL cru")]
    public async Task IndiceUnico_MesmaCategoriaDuasVezesNaMesmaBanca_Recusa()
    {
        Guid processoId = await GravarAsync("PS candidatos atendidos unico", Guid.CreateVersion7(), BancaComCandidatosAtendidos(Guid.CreateVersion7(), "RENDA"));

        await using UniPlusDbContext direto = _fixture.CreateDbContext();
        Guid bancaId = (await CarregarAsync(direto, processoId)).CronogramaFases.Single().BancasRequeridas.Single().Id;

        // A recusa no agregado é check-then-act não-atômico: duas requisições concorrentes
        // sobre a mesma banca passariam as duas pela leitura. A constraint é a defesa que
        // não depende de ordem.
        Func<Task> inserirDuplicata = async () => await direto.Database.ExecuteSqlAsync(
            $"INSERT INTO selecao.categorias_julgadas (id, banca_requerida_id, categoria_documento_origem_id, codigo, nome, created_at) VALUES ({Guid.CreateVersion7()}, {bancaId}, {Guid.CreateVersion7()}, {"RENDA"}, {"Renda"}, {DateTimeOffset.UtcNow})");

        Npgsql.PostgresException violacao = (await inserirDuplicata.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        violacao.SqlState.Should().Be("23505");
        violacao.ConstraintName.Should().Be("ux_categorias_julgadas_codigo");
    }

    // A banca antiga foi removida com a reposição; os candidatos atendidos dela não podem
    // sobreviver ao pai, ou o certame passaria a declarar competência de uma banca
    // que ninguém mais requer.
    private static async Task ConferirQueNenhumaCategoriaFicouOrfaAsync(UniPlusDbContext db)
    {
        List<Guid> bancasVivas = await db.BancasRequeridas.Select(b => b.Id).ToListAsync();
        (await db.Set<CategoriaJulgada>().CountAsync(c => !bancasVivas.Contains(c.BancaRequeridaId)))
            .Should().Be(0, "nenhuma categoria julgada sobrevive à banca que a declarou");
    }

    private static BancaRequerida BancaComCandidatosAtendidos(Guid tipoBanca, string categoriaCodigo) =>
        BancaRequerida.Criar(FormacaoDeChaveLocal.Formar("banca", categoriaCodigo, new ChavesEmUso()),
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, tipoBanca),
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), categoriaCodigo, categoriaCodigo)]);

    private async Task<Guid> GravarAsync(string nome, Guid faseCanonicaOrigemId, params BancaRequerida[] bancas)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso($"{nome} {Guid.CreateVersion7()}");
        processo.DefinirCronogramaFases([Fase(faseCanonicaOrigemId, bancas)], [])
            .IsSuccess.Should().BeTrue();
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();
        return processo.Id;
    }

    private async Task RedefinirAsync(Guid processoId, Guid faseCanonicaOrigemId, params BancaRequerida[] bancas)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);
        Result redefinicao = processo.DefinirCronogramaFases([Fase(faseCanonicaOrigemId, bancas)], []);
        redefinicao.IsSuccess.Should().BeTrue(redefinicao.Error?.Message);
        await db.SaveChangesAsync();
    }

    private static ProcessoSeletivo NovoProcesso(string nome) => ProcessoSeletivo.Criar(
        nome, TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FaseCronograma Fase(Guid faseCanonicaOrigemId, IReadOnlyList<BancaRequerida> bancas) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(
                origemId: faseCanonicaOrigemId, codigo: "ANALISE_DOCUMENTAL", origemData: OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false,
                coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: bancas).Value!;

    private static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.CandidatosAtendidos)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}
