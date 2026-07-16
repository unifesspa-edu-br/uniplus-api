namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Integração ponta-a-ponta da Story #460 contra Postgres real: cobertura
/// dos cenários BDD (CA-03 histórico em transação, CA-05 hash determinístico
/// cross-run, CA-02 UNIQUE parcial) e CA-06 (recomputação via interceptor).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class ObrigatoriedadeLegalPersistenceTests : IClassFixture<ObrigatoriedadeLegalDbFixture>
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ObrigatoriedadeLegalDbFixture _fixture;

    public ObrigatoriedadeLegalPersistenceTests(ObrigatoriedadeLegalDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Insert gera linha em obrigatoriedade_legal_historico na mesma transação (CA-03/CA-06)")]
    public async Task Insert_GeraHistoricoNaMesmaTransacao()
    {
        ObrigatoriedadeLegal regra = NovaRegraValida("ETAPA_OBRIGATORIA_INSERT");

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        await using SelecaoDbContext readCtx = _fixture.CreateDbContext(userId: null);

        ObrigatoriedadeLegal persisted = await readCtx.ObrigatoriedadesLegais
            .SingleAsync(o => o.Id == regra.Id)
            ;

        persisted.Hash.Should().Be(regra.Hash);
        HashCanonicalComputer.IsValidHashShape(persisted.Hash).Should().BeTrue();

        List<ObrigatoriedadeLegalHistorico> historico = await readCtx.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == regra.Id)
            .ToListAsync()
            ;

        historico.Should().HaveCount(1);
        historico[0].Hash.Should().Be(regra.Hash);
        historico[0].SnapshotBy.Should().Be(AdminA);
        historico[0].ConteudoJson.Should().Contain(regra.Hash,
            "snapshot canônico deve incluir o hash recomputado da regra");
        historico[0].ConteudoJson.Should().Contain("\"tipoProcessoCodigo\"",
            "a chave renomeada precisa estar no payload forense");
        historico[0].ConteudoJson.Should().NotContain("tipoEditalCodigo",
            "a chave legada não pode sobreviver no histórico pós-rename");
    }

    [Fact(DisplayName = "Update gera 2ª linha de histórico e atualiza Hash quando BaseLegal muda")]
    public async Task Update_GeraSegundaLinhaDeHistorico()
    {
        ObrigatoriedadeLegal regra = NovaRegraValida("ETAPA_UPDATE");

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        string hashOriginal = regra.Hash;

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            ObrigatoriedadeLegal tracked = await ctx.ObrigatoriedadesLegais
                .SingleAsync(o => o.Id == regra.Id)
                ;

            tracked.Atualizar(
                tipoProcessoCodigo: tracked.TipoProcessoCodigo,
                categoria: tracked.Categoria,
                regraCodigo: tracked.RegraCodigo,
                predicado: tracked.Predicado,
                descricaoHumana: tracked.DescricaoHumana,
                baseLegal: "Lei 14.723/2023 art.2º — atualizada",
                vigenciaInicio: tracked.VigenciaInicio,
                vigenciaFim: tracked.VigenciaFim,
                atoNormativoUrl: tracked.AtoNormativoUrl,
                portariaInternaCodigo: tracked.PortariaInternaCodigo);

            await ctx.SaveChangesAsync();
        }

        await using SelecaoDbContext readCtx = _fixture.CreateDbContext(userId: null);

        ObrigatoriedadeLegal atualizada = await readCtx.ObrigatoriedadesLegais
            .SingleAsync(o => o.Id == regra.Id)
            ;

        atualizada.Hash.Should().NotBe(hashOriginal);

        List<ObrigatoriedadeLegalHistorico> historico = await readCtx.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == regra.Id)
            .OrderBy(h => h.SnapshotAt)
            .ToListAsync()
            ;

        historico.Should().HaveCount(2);
        historico[0].Hash.Should().Be(hashOriginal);
        historico[0].SnapshotBy.Should().Be(AdminA);
        historico[1].Hash.Should().Be(atualizada.Hash);
        historico[1].SnapshotBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "UNIQUE parcial sobre Hash rejeita duas regras ativas com mesmo conteúdo (CA-02)")]
    public async Task UniquePartial_RejeitaDuasRegrasAtivasComMesmoHash()
    {
        ObrigatoriedadeLegal regra1 = NovaRegraValida("ETAPA_HASH_UNIQUE");

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra1);
            await ctx.SaveChangesAsync();
        }

        // Mesma "semântica" mas Id v7 diferente — hash idêntico (CA-05).
        ObrigatoriedadeLegal regra2 = NovaRegraValida("ETAPA_HASH_UNIQUE");
        regra1.Hash.Should().Be(regra2.Hash,
            "duas regras com conteúdo semântico idêntico precisam compartilhar hash");

        await using SelecaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.ObrigatoriedadesLegais.Add(regra2);

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException))
            ;
    }

    [Fact(DisplayName = "Soft-delete liberta o slot da UNIQUE — regra nova com mesmo hash é aceita")]
    public async Task SoftDelete_LibertaUniquePartial()
    {
        ObrigatoriedadeLegal regra = NovaRegraValida("ETAPA_HASH_RECYCLE");

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        // Soft-delete via SoftDeleteInterceptor.
        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ObrigatoriedadeLegal tracked = await ctx.ObrigatoriedadesLegais
                .SingleAsync(o => o.Id == regra.Id)
                ;
            ctx.ObrigatoriedadesLegais.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        ObrigatoriedadeLegal regraNova = NovaRegraValida("ETAPA_HASH_RECYCLE");

        await using SelecaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.ObrigatoriedadesLegais.Add(regraNova);

        // Sem exceção — UNIQUE parcial WHERE is_deleted = false permite reuso.
        await ctx3.SaveChangesAsync();
    }

    [Fact(DisplayName = "Soft-delete gera linha em histórico com isDeleted=true (CA-06 — desativação)")]
    public async Task SoftDelete_GeraLinhaDeHistorico()
    {
        ObrigatoriedadeLegal regra = NovaRegraValida("ETAPA_SOFT_DELETE_HISTORY");

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            ObrigatoriedadeLegal tracked = await ctx.ObrigatoriedadesLegais
                .SingleAsync(o => o.Id == regra.Id);
            ctx.ObrigatoriedadesLegais.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        // Bypass do query filter para verificar o estado real persistido —
        // após soft-delete, a regra continua no banco com IsDeleted=true.
        await using SelecaoDbContext readCtx = _fixture.CreateDbContext(userId: null);

        List<ObrigatoriedadeLegalHistorico> historico = await readCtx.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == regra.Id)
            .OrderBy(h => h.SnapshotAt)
            .ToListAsync();

        historico.Should().HaveCount(2,
            "CA-06 exige snapshot por mutação — Insert + soft-delete = 2 linhas");
        historico[0].SnapshotBy.Should().Be(AdminA);
        historico[1].SnapshotBy.Should().Be(AdminB);

        // jsonb no Postgres normaliza espaçamento ao re-serializar — comparação
        // por substring com espaço acoplaria o teste ao formato de saída do
        // banco. Desserializar para verificar a propriedade semântica.
        using JsonDocument snapshotDeletado = JsonDocument.Parse(historico[1].ConteudoJson);
        snapshotDeletado.RootElement.GetProperty("isDeleted").GetBoolean().Should().BeTrue(
            "snapshot do soft-delete deve refletir IsDeleted=true no payload canônico");
    }

    [Fact(DisplayName = "Hash é determinístico cross-run (CA-05) — mesmo conteúdo persistido por processos diferentes")]
    public async Task Hash_DeterministicoCrossRun()
    {
        ObrigatoriedadeLegal regra = NovaRegraValida("ETAPA_DETERMINISTIC");

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        // Reconstrói o mesmo conteúdo em uma nova instância e re-computa
        // o hash sem passar pela persistência — deve bater com o persistido.
        string hashCalculadoDeNovo = HashCanonicalComputer.Compute(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_DETERMINISTIC",
            predicado: new EtapaObrigatoria("ProvaObjetiva"),
            baseLegal: "Lei 12.711/2012 art.1º",
            portariaInternaCodigo: null,
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null);

        await using SelecaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        string persistido = (await readCtx.ObrigatoriedadesLegais
            .SingleAsync(o => o.Id == regra.Id)
            ).Hash;

        persistido.Should().Be(hashCalculadoDeNovo,
            "o hash do banco precisa bater com o computado fresh — invariante de evidência forense");
    }

    [Fact(DisplayName = "Ruleset reúne universal e específico, exclui outro tipo e respeita a data explícita")]
    public async Task ObterVigentesParaTipoProcesso_UniaoEDataReferencia()
    {
        ObrigatoriedadeLegal universal = NovaRegraValida("RULESET_UNIVERSAL");
        ObrigatoriedadeLegal psiq = ObrigatoriedadeLegal.Criar(
            "PSIQ", CategoriaObrigatoriedade.Outros, "RULESET_PSIQ", new ConcorrenciaDuplaObrigatoria(),
            "PSIQ", "Lei", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1)).Value!;
        ObrigatoriedadeLegal sisu = ObrigatoriedadeLegal.Criar(
            "SiSU", CategoriaObrigatoriedade.Outros, "RULESET_SISU", new ConcorrenciaDuplaObrigatoria(),
            "SiSU", "Lei", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1)).Value!;

        await using (SelecaoDbContext context = _fixture.CreateDbContext(AdminA))
        {
            context.ObrigatoriedadesLegais.AddRange(universal, psiq, sisu);
            await context.SaveChangesAsync();
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext(userId: null);
        ObrigatoriedadeLegalRepository repository = new(readContext, TimeProvider.System);

        IReadOnlyList<ObrigatoriedadeLegal> emVigencia = await repository
            .ObterVigentesParaTipoProcessoAsync("PSIQ", new DateOnly(2026, 6, 15));
        IReadOnlyList<ObrigatoriedadeLegal> foraDaVigencia = await repository
            .ObterVigentesParaTipoProcessoAsync("PSIQ", new DateOnly(2026, 7, 5));

        emVigencia.Select(static regra => regra.RegraCodigo).Should().Contain("RULESET_UNIVERSAL");
        emVigencia.Select(static regra => regra.RegraCodigo).Should().Contain("RULESET_PSIQ");
        emVigencia.Select(static regra => regra.RegraCodigo).Should().NotContain("RULESET_SISU");
        foraDaVigencia.Select(static regra => regra.RegraCodigo).Should().Contain("RULESET_UNIVERSAL");
        foraDaVigencia.Select(static regra => regra.RegraCodigo).Should().NotContain("RULESET_PSIQ");
    }

    [Fact(DisplayName = "FK historico→regra bloqueia INSERT de histórico órfão (Codex P2)")]
    public async Task FkHistorico_BloqueiaOrfao()
    {
        // Tentar inserir uma linha de histórico apontando para regra_id que
        // não existe — o FK ON DELETE RESTRICT precisa rejeitar via Postgres.
        Guid regraInexistente = Guid.CreateVersion7();

        ObrigatoriedadeLegalHistorico orphan = ObrigatoriedadeLegalHistorico.Snapshot(
            regraId: regraInexistente,
            conteudoJson: "{}",
            hash: new string('a', 64),
            snapshotAt: DateTimeOffset.UtcNow,
            snapshotBy: AdminA);

        await using SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.ObrigatoriedadeLegalHistorico.Add(orphan);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException))
            ;
    }

    private static ObrigatoriedadeLegal NovaRegraValida(string regraCodigo) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: regraCodigo,
            predicado: new EtapaObrigatoria("ProvaObjetiva"),
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;
}
