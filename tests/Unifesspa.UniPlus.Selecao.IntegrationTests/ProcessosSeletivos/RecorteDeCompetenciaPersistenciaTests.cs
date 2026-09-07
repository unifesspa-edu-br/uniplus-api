namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

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
        Guid categoriaRenda = Guid.CreateVersion7();
        Guid categoriaEtnicoRacial = Guid.CreateVersion7();
        Guid processoId;

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS recorte {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [Fase(Guid.CreateVersion7(),
                [
                    BancaRequerida.Criar(tipoUnico, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(categoriaRenda, "RENDA")]),
                    BancaRequerida.Criar(tipoUnico, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(categoriaEtnicoRacial, "ETNICO_RACIAL")]),
                ])],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.SaveChangesAsync();
            processoId = processo.Id;
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);
            IReadOnlyCollection<BancaRequerida> bancas = processo.CronogramaFases.Single().BancasRequeridas;

            bancas.Should().HaveCount(2);
            bancas.Should().OnlyContain(b => b.Codigo == "BANCA_ANALISE_DOCUMENTAL" && b.TipoBancaOrigemId == tipoUnico);
            bancas.SelectMany(b => b.RecorteDeCompetencia).Select(c => c.Codigo)
                .Should().BeEquivalentTo(["RENDA", "ETNICO_RACIAL"],
                    "cada banca guarda o próprio recorte — é ele que separa uma da outra no snapshot publicado");
            bancas.SelectMany(b => b.RecorteDeCompetencia)
                .Should().OnlyContain(c => c.BancaRequeridaId != Guid.Empty);
        }
    }

    [Fact(DisplayName = "Regravar o cronograma repõe o recorte sem deixar categoria órfã da banca que saiu")]
    public async Task RedefinirCronograma_OutroRecorte_SubstituiSemDeixarOrfa()
    {
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        Guid tipoBanca = Guid.CreateVersion7();
        Guid processoId;

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS recorte reposto {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId,
                [
                    BancaRequerida.Criar(tipoBanca, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")]),
                ])],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.SaveChangesAsync();
            processoId = processo.Id;
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);

            Result redefinicao = processo.DefinirCronogramaFases(
                [Fase(faseCanonicaOrigemId,
                [
                    BancaRequerida.Criar(tipoBanca, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(Guid.CreateVersion7(), "ESCOLARIDADE")]),
                ])],
                [],
                PrecondicaoIfMatch.Curinga);

            redefinicao.IsSuccess.Should().BeTrue(redefinicao.Error?.Message);
            await db.SaveChangesAsync();
        }

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await CarregarAsync(db, processoId);

            processo.CronogramaFases.Single().BancasRequeridas.Single()
                .RecorteDeCompetencia.Should().ContainSingle()
                .Which.Codigo.Should().Be("ESCOLARIDADE");

            // A banca antiga foi removida com a reposição; o recorte dela não pode
            // sobreviver ao pai, ou o certame passaria a declarar competência de uma banca
            // que ninguém mais requer.
            List<Guid> bancasVivas = await db.BancasRequeridas.Select(b => b.Id).ToListAsync();
            (await db.Set<CategoriaJulgada>().CountAsync(c => !bancasVivas.Contains(c.BancaRequeridaId)))
                .Should().Be(0, "nenhuma categoria julgada sobrevive à banca que a declarou");
        }
    }

    [Fact(DisplayName = "O índice único recusa a mesma categoria duas vezes na mesma banca, via SQL cru")]
    public async Task IndiceUnico_MesmaCategoriaDuasVezesNaMesmaBanca_Recusa()
    {
        Guid processoId;
        Guid bancaId;

        await using (SelecaoDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS recorte unico {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [Fase(Guid.CreateVersion7(),
                [
                    BancaRequerida.Criar(Guid.CreateVersion7(), "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")]),
                ])],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.SaveChangesAsync();
            processoId = processo.Id;
            bancaId = processo.CronogramaFases.Single().BancasRequeridas.Single().Id;
        }

        processoId.Should().NotBeEmpty();

        // A recusa no agregado é check-then-act não-atômico: duas requisições concorrentes
        // sobre a mesma banca passariam as duas pela leitura. A constraint é a defesa que
        // não depende de ordem.
        await using SelecaoDbContext direto = _fixture.CreateDbContext();
        Func<Task> inserirDuplicata = async () => await direto.Database.ExecuteSqlAsync(
            $"INSERT INTO selecao.categorias_julgadas (id, banca_requerida_id, categoria_documento_origem_id, codigo, created_at) VALUES ({Guid.CreateVersion7()}, {bancaId}, {Guid.CreateVersion7()}, {"RENDA"}, {DateTimeOffset.UtcNow})");

        Npgsql.PostgresException violacao = (await inserirDuplicata.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        violacao.SqlState.Should().Be("23505");
        violacao.ConstraintName.Should().Be("ux_categorias_julgadas_codigo");
    }

    private static ProcessoSeletivo NovoProcesso(string nome) => ProcessoSeletivo.Criar(
        nome, TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static FaseCronograma Fase(Guid faseCanonicaOrigemId, IReadOnlyList<BancaRequerida> bancas) =>
        FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: faseCanonicaOrigemId,
            codigo: "ANALISE_DOCUMENTAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Delegada,
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
            regraRecurso: null).Value!;

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);
}
