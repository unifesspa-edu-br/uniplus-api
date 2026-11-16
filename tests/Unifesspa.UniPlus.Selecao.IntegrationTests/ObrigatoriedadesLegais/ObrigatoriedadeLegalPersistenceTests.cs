namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
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

    [Fact(DisplayName = "Insert gera linha em historicos_da_obrigatoriedade na mesma transação (CA-03/CA-06)")]
    public async Task Insert_GeraHistoricoNaMesmaTransacao()
    {
        ObrigatoriedadeLegal regra = NovaRegraValida("ETAPA_OBRIGATORIA_INSERT");

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);

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
        await GravarAsync(AdminA, regra);
        string hashOriginal = regra.Hash;

        await AtualizarBaseLegalAsync(AdminB, regra.Id, "Lei 14.723/2023 art.2º — atualizada");

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        ObrigatoriedadeLegal atualizada = await readCtx.ObrigatoriedadesLegais.SingleAsync(o => o.Id == regra.Id);
        List<ObrigatoriedadeLegalHistorico> historico = await readCtx.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == regra.Id).OrderBy(h => h.SnapshotAt).ToListAsync();
        atualizada.Hash.Should().NotBe(hashOriginal);
        historico.Select(h => (h.Hash, h.SnapshotBy)).Should().Equal((hashOriginal, AdminA), (atualizada.Hash, AdminB));
    }

    [Fact(DisplayName = "UNIQUE parcial sobre Hash rejeita duas regras ativas com mesmo conteúdo (CA-02)")]
    public async Task UniquePartial_RejeitaDuasRegrasAtivasComMesmoHash()
    {
        ObrigatoriedadeLegal regra1 = NovaRegraValida("ETAPA_HASH_UNIQUE");

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra1);
            await ctx.SaveChangesAsync();
        }

        // Mesma "semântica" mas Id v7 diferente — hash idêntico (CA-05).
        ObrigatoriedadeLegal regra2 = NovaRegraValida("ETAPA_HASH_UNIQUE");
        regra1.Hash.Should().Be(regra2.Hash,
            "duas regras com conteúdo semântico idêntico precisam compartilhar hash");

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
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

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        // Soft-delete via SoftDeleteInterceptor.
        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ObrigatoriedadeLegal tracked = await ctx.ObrigatoriedadesLegais
                .SingleAsync(o => o.Id == regra.Id)
                ;
            ctx.ObrigatoriedadesLegais.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        ObrigatoriedadeLegal regraNova = NovaRegraValida("ETAPA_HASH_RECYCLE");

        await using UniPlusDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.ObrigatoriedadesLegais.Add(regraNova);

        // Sem exceção — UNIQUE parcial WHERE is_deleted = false permite reuso.
        await ctx3.SaveChangesAsync();
    }

    [Fact(DisplayName = "Soft-delete gera linha em histórico com isDeleted=true (CA-06 — desativação)")]
    public async Task SoftDelete_GeraLinhaDeHistorico()
    {
        ObrigatoriedadeLegal regra = NovaRegraValida("ETAPA_SOFT_DELETE_HISTORY");

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.ObrigatoriedadesLegais.Add(regra);
            await ctx.SaveChangesAsync();
        }

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            ObrigatoriedadeLegal tracked = await ctx.ObrigatoriedadesLegais
                .SingleAsync(o => o.Id == regra.Id);
            ctx.ObrigatoriedadesLegais.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        // Bypass do query filter para verificar o estado real persistido —
        // após soft-delete, a regra continua no banco com IsDeleted=true.
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);

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

        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
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

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        string persistido = (await readCtx.ObrigatoriedadesLegais
            .SingleAsync(o => o.Id == regra.Id)
            ).Hash;

        persistido.Should().Be(hashCalculadoDeNovo,
            "o hash do banco precisa bater com o computado fresh — invariante de evidência forense");
    }

    [Fact(DisplayName = "Ruleset reúne universal e específico, exclui outro tipo e respeita a data explícita")]
    public async Task ObterVigentesParaTipoProcesso_UniaoEDataReferencia()
    {
        await GravarAsync(
            AdminA, NovaRegraValida("RULESET_UNIVERSAL"), RegraDeJunho("PSIQ", "RULESET_PSIQ"), RegraDeJunho("SiSU", "RULESET_SISU"));

        IReadOnlyList<string> emVigencia = await CodigosVigentesAsync("PSIQ", new DateOnly(2026, 6, 15));
        IReadOnlyList<string> foraDaVigencia = await CodigosVigentesAsync("PSIQ", new DateOnly(2026, 7, 5));

        emVigencia.Should().Contain(["RULESET_UNIVERSAL", "RULESET_PSIQ"]).And.NotContain("RULESET_SISU");
        foraDaVigencia.Should().Contain("RULESET_UNIVERSAL").And.NotContain("RULESET_PSIQ");
    }

    [Fact(DisplayName = "FK historico→regra bloqueia INSERT de histórico órfão")]
    public async Task FkHistorico_BloqueiaOrfao()
    {
        // O FK ON DELETE RESTRICT precisa rejeitar, via Postgres, um histórico que aponta para regra inexistente.
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.ObrigatoriedadeLegalHistorico.Add(HistoricoDeRegraInexistente());

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>().WithInnerException(typeof(Npgsql.PostgresException));
    }

    private static ObrigatoriedadeLegalHistorico HistoricoDeRegraInexistente() =>
        ObrigatoriedadeLegalHistorico.Snapshot(
            regraId: Guid.CreateVersion7(),
            conteudoJson: "{}",
            hash: new string('a', 64),
            snapshotAt: DateTimeOffset.UtcNow,
            snapshotBy: AdminA);

    private async Task GravarAsync(string usuario, params ObrigatoriedadeLegal[] regras)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ctx.ObrigatoriedadesLegais.AddRange(regras);
        await ctx.SaveChangesAsync();
    }

    private async Task AtualizarBaseLegalAsync(string usuario, Guid id, string baseLegal)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ObrigatoriedadeLegal regra = await ctx.ObrigatoriedadesLegais.SingleAsync(o => o.Id == id);
        regra.Atualizar(
            regra.TipoProcessoCodigo,
            regra.Categoria,
            regra.RegraCodigo,
            regra.Predicado,
            new TextoNormativo(regra.DescricaoHumana, baseLegal, regra.AtoNormativoUrl, regra.PortariaInternaCodigo),
            regra.VigenciaInicio,
            regra.VigenciaFim);
        await ctx.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> CodigosVigentesAsync(string tipoProcesso, DateOnly data)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext(userId: null);
        ObrigatoriedadeLegalRepository repository = new(readContext, TimeProvider.System);
        IReadOnlyList<ObrigatoriedadeLegal> vigentes = await repository.ObterVigentesParaTipoProcessoAsync(tipoProcesso, data);
        return [.. vigentes.Select(static regra => regra.RegraCodigo)];
    }

    private static ObrigatoriedadeLegal RegraDeJunho(string tipoProcesso, string regraCodigo) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcesso,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            new ConcorrenciaDuplaObrigatoria(),
            new TextoNormativo(tipoProcesso, "Lei", null, null),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 7, 1)).Value!;

    private static ObrigatoriedadeLegal NovaRegraValida(string regraCodigo) =>
        ObrigatoriedadeLegal.Criar(
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            CategoriaObrigatoriedade.Etapa,
            regraCodigo,
            new EtapaObrigatoria("ProvaObjetiva"),
            new TextoNormativo("Edital deve incluir etapa de Prova Objetiva.", "Lei 12.711/2012 art.1º", null, null),
            new DateOnly(2026, 1, 1),
            null).Value!;
}
