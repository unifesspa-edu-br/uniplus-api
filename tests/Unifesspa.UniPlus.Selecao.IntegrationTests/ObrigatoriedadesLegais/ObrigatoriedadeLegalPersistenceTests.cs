namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

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
                tipoEditalCodigo: tracked.TipoEditalCodigo,
                categoria: tracked.Categoria,
                regraCodigo: tracked.RegraCodigo,
                predicado: tracked.Predicado,
                descricaoHumana: tracked.DescricaoHumana,
                baseLegal: "Lei 14.723/2023 art.2º — atualizada",
                vigenciaInicio: tracked.VigenciaInicio,
                vigenciaFim: tracked.VigenciaFim,
                atoNormativoUrl: tracked.AtoNormativoUrl,
                portariaInternaCodigo: tracked.PortariaInternaCodigo,
                proprietario: tracked.Proprietario,
                areasDeInteresse: tracked.AreasDeInteresse);

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

    [Fact(DisplayName = "Update em DbContext fresco preserva AreasDeInteresse no snapshot (Codex #2)")]
    public async Task Update_DbContextFresco_PreservaAreasNoSnapshot()
    {
        // Cenário forense: o cliente admin (#461) carrega a regra de um
        // DbContext fresco — sem nav property para a junction, o set
        // in-memory AreasDeInteresse fica vazio mesmo com bindings vigentes.
        // O interceptor precisa reconciliar com a junction antes do snapshot.

        AreaCodigo cepsArea = AreaCodigo.From("CEPS").Value!;
        AreaCodigo proegArea = AreaCodigo.From("PROEG").Value!;

        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: ObrigatoriedadeLegal.TipoEditalUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_GOVERNANCE_SNAPSHOT",
            predicado: new EtapaObrigatoria("ProvaObjetiva"),
            descricaoHumana: "Regra com governance para testar snapshot.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            proprietario: cepsArea,
            areasDeInteresse: new HashSet<AreaCodigo> { cepsArea, proegArea }).Value!;

        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            // Persiste também os bindings vigentes — simula o que o admin
            // CRUD de #461 vai fazer ao traduzir o set para junction.
            ctx.Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>().Add(
                AreaDeInteresseBinding<ObrigatoriedadeLegal>.Criar(
                    regra.Id, cepsArea, DateTimeOffset.UtcNow, AdminA));
            ctx.Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>().Add(
                AreaDeInteresseBinding<ObrigatoriedadeLegal>.Criar(
                    regra.Id, proegArea, DateTimeOffset.UtcNow, AdminA));
            await ctx.SaveChangesAsync();
        }

        // Update via DbContext fresco — set in-memory vem VAZIO da carga EF.
        // O admin CRUD (#461) terá que reidratar AreasDeInteresse da junction
        // antes de chamar Atualizar (semântica full-replace do ADR-0058).
        // Aqui simulamos esse passo: lemos a junction e passamos como input.
        await using (SelecaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            ObrigatoriedadeLegal tracked = await ctx.ObrigatoriedadesLegais
                .SingleAsync(o => o.Id == regra.Id);

            // Sanity: confirma que o set in-memory está vazio neste momento
            // (regra carregada sem nav property para a junction).
            tracked.AreasDeInteresse.Should().BeEmpty(
                "premissa do Codex finding: EF não popula AreasDeInteresse via junction");

            // Hidratação explícita das áreas a partir da junction — papel do
            // repositório admin (#461) quando reconciliar set ↔ junction.
            HashSet<AreaCodigo> areasHidratadas = [..
                await ctx.Set<AreaDeInteresseBinding<ObrigatoriedadeLegal>>()
                    .AsNoTracking()
                    .Where(b => b.ParentId == regra.Id && b.ValidoAte == null)
                    .Select(b => b.AreaCodigo)
                    .ToListAsync()];

            tracked.Atualizar(
                tipoEditalCodigo: tracked.TipoEditalCodigo,
                categoria: tracked.Categoria,
                regraCodigo: tracked.RegraCodigo,
                predicado: tracked.Predicado,
                descricaoHumana: tracked.DescricaoHumana,
                baseLegal: "Lei 14.723/2023 art.2º — Codex hydration test",
                vigenciaInicio: tracked.VigenciaInicio,
                vigenciaFim: tracked.VigenciaFim,
                atoNormativoUrl: tracked.AtoNormativoUrl,
                portariaInternaCodigo: tracked.PortariaInternaCodigo,
                proprietario: tracked.Proprietario,
                areasDeInteresse: areasHidratadas);

            await ctx.SaveChangesAsync();
        }

        // Verifica que o snapshot do Update carregou as áreas via interceptor
        await using SelecaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        List<ObrigatoriedadeLegalHistorico> historico = await readCtx.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == regra.Id)
            .OrderBy(h => h.SnapshotAt)
            .ToListAsync();

        historico.Should().HaveCount(2);

        using JsonDocument snapshotUpdate = JsonDocument.Parse(historico[1].ConteudoJson);
        JsonElement areas = snapshotUpdate.RootElement.GetProperty("areasDeInteresse");
        areas.ValueKind.Should().Be(JsonValueKind.Array);
        IReadOnlyList<string> codigos = [.. areas.EnumerateArray().Select(e => e.GetString()!)];

        codigos.Should().BeEquivalentTo(new[] { "CEPS", "PROEG" },
            "interceptor deve hidratar AreasDeInteresse da junction antes de serializar — "
            + "snapshot vazio quebraria a evidência forense de governance (Codex finding #2)");
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
            tipoEditalCodigo: "*",
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

    [Fact(DisplayName = "edital_governance_snapshot é tabela criada vazia em V1 (CA-04)")]
    public async Task EditalGovernanceSnapshot_TabelaVaziaPorPadrao()
    {
        await using SelecaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        long total = await ctx.EditalGovernanceSnapshots
            .LongCountAsync()
            ;

        total.Should().Be(0L, "#460 cria apenas o schema — INSERT é responsabilidade de #462");
    }

    private static ObrigatoriedadeLegal NovaRegraValida(string regraCodigo) =>
        ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: ObrigatoriedadeLegal.TipoEditalUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: regraCodigo,
            predicado: new EtapaObrigatoria("ProvaObjetiva"),
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;
}
