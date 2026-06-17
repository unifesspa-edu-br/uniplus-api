namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Campi;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;
using Unifesspa.UniPlus.Configuracao.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Integração ponta-a-ponta dos cadastros contra Postgres real (UNI-REQ #587):
/// CA-02 (persistência por código + display cache, sem FK ao Geo), UNIQUE parcial
/// de sigla, soft-delete e bloqueio de remoção de Campus com LocalOferta vivo.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CampusPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    public CampusPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-02: criar Campus persiste a referência de cidade por código + display cache (sem FK ao Geo)")]
    public async Task Insert_PersisteReferenciaCidadeEDisplayCache()
    {
        Campus campus = NovoCampus("CAMar", "Campus Marabá", "1504208", "Marabá", "PA");

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Campi.Add(campus);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Campus persistido = await readCtx.Campi.SingleAsync(c => c.Id == campus.Id);

        persistido.Sigla.Should().Be("CAMAR");
        persistido.CidadeCodigoIbge.Should().Be("1504208");
        persistido.CidadeNome.Should().Be("Marabá");
        persistido.CidadeUf.Should().Be("PA");
        persistido.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        persistido.CidadeDisplayAtualizadoEm.Should().Be(Agora);
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "UNIQUE parcial (sigla) rejeita segundo Campus vivo com a mesma sigla")]
    public async Task UniquePartial_Sigla_RejeitaDuplicataAtiva()
    {
        Campus primeiro = NovoCampus("DUPSIG", "Campus Um", "1504208", "Marabá", "PA");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Campi.Add(primeiro);
            await ctx.SaveChangesAsync();
        }

        Campus segundo = NovoCampus("DUPSIG", "Campus Dois", "1501402", "Belém", "PA");
        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Campi.Add(segundo);

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException));
    }

    [Fact(DisplayName = "Soft-delete liberta o slot da UNIQUE parcial de sigla")]
    public async Task SoftDelete_LibertaUniquePartialSigla()
    {
        Campus campus = NovoCampus("RECSIG", "Campus Reciclado", "1504208", "Marabá", "PA");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Campi.Add(campus);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            Campus tracked = await ctx.Campi.SingleAsync(c => c.Id == campus.Id);
            ctx.Campi.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            Campus excluido = await ctx.Campi.IgnoreQueryFilters().SingleAsync(c => c.Id == campus.Id);
            excluido.IsDeleted.Should().BeTrue();
            excluido.DeletedBy.Should().Be(AdminB);
        }

        Campus novo = NovoCampus("RECSIG", "Campus Novo", "1501402", "Belém", "PA");
        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.Campi.Add(novo);

        Func<Task> act = async () => await ctx3.SaveChangesAsync();

        await act.Should().NotThrowAsync("o slot da sigla foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CA-05: remover Campus responsável por LocalOferta vivo é bloqueado (409)")]
    public async Task RemoverCampus_ComLocalOfertaVivo_Bloqueia()
    {
        Campus campus = NovoCampus("BLKCAM", "Campus Bloqueado", "1504208", "Marabá", "PA");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Campi.Add(campus);
            await ctx.SaveChangesAsync();
        }

        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CursoForaDeSede, campus.Id, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.LocaisOferta.Add(local);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext handlerCtx = _fixture.CreateDbContext(AdminB);
        var campusRepo = new CampusRepository(handlerCtx);
        var localRepo = new LocalOfertaRepository(handlerCtx);

        Result resultado = await RemoverCampusCommandHandler.Handle(
            new RemoverCampusCommand(campus.Id), campusRepo, localRepo, handlerCtx, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.RemocaoBloqueadaPorLocalOferta);
    }

    [Fact(DisplayName = "FK campus_responsavel_id com RESTRICT bloqueia DELETE físico do Campus com LocalOferta")]
    public async Task FkRestrict_BloqueiaDeleteFisicoDoCampus()
    {
        Campus campus = NovoCampus("FKCAM", "Campus FK", "1504208", "Marabá", "PA");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Campi.Add(campus);
            await ctx.SaveChangesAsync();
        }

        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CursoForaDeSede, campus.Id, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.LocaisOferta.Add(local);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext rawCtx = _fixture.CreateDbContext(userId: null);
        Func<Task> act = async () =>
            await rawCtx.Database.ExecuteSqlAsync($"DELETE FROM campus WHERE id = {campus.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a FK campus_responsavel_id com RESTRICT impede o DELETE físico do campus referenciado");
    }

    private static Campus NovoCampus(string sigla, string nome, string codigoIbge, string cidadeNome, string uf) =>
        Campus.Criar(
            sigla, nome, codigoIbge, cidadeNome, uf,
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null, null, null, null).Value!;
}
