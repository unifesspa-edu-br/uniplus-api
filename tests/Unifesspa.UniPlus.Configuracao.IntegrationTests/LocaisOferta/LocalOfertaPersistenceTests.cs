namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.LocaisOferta;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class LocalOfertaPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    public LocalOfertaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-02: criar LocalOferta persiste tipo, campus responsável e referência de cidade por código")]
    public async Task Insert_PersisteFlatComReferenciaCidade()
    {
        Campus campus = Campus.Criar(
            "LOFCAM", "Campus Local", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Campi.Add(campus);
            await ctx.SaveChangesAsync();
        }

        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.PoloEad, campus.Id, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, "55555").Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.LocaisOferta.Add(local);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        LocalOferta persistido = await readCtx.LocaisOferta.SingleAsync(l => l.Id == local.Id);

        persistido.Tipo.Should().Be(TipoLocalOferta.PoloEad);
        persistido.CampusResponsavelId.Should().Be(campus.Id);
        persistido.CidadeCodigoIbge.Should().Be("1504208");
        persistido.CidadeUf.Should().Be("PA");
        persistido.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        persistido.CreatedBy.Should().Be(AdminA);
    }

    [Fact(DisplayName = "LocalOferta sem campus responsável persiste (FK intra-banco opcional, ADR-0065)")]
    public async Task Insert_SemCampusResponsavel_Persiste()
    {
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.ConvenioInteriorizacao, null, "1501402", "Belém", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.LocaisOferta.Add(local);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        LocalOferta persistido = await readCtx.LocaisOferta.SingleAsync(l => l.Id == local.Id);

        persistido.CampusResponsavelId.Should().BeNull();
    }

    [Fact(DisplayName = "CA-01/CA-07: criar LocalOferta com endereço estruturado faz round-trip das colunas owned")]
    public async Task Insert_ComEndereco_RoundTrip()
    {
        ReferenciaEnderecoGeo endereco = ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            "1504208", "Marabá", "PA", -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).Value!;

        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, endereco, null).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.LocaisOferta.Add(local);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        LocalOferta persistido = await readCtx.LocaisOferta.SingleAsync(l => l.Id == local.Id);

        persistido.Endereco.Should().NotBeNull();
        persistido.Endereco!.Cep.Should().Be("68507590");
        persistido.Endereco.CidadeCodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "FK campus_responsavel_id inexistente é rejeitada pelo banco")]
    public async Task Insert_CampusResponsavelInexistente_RejeitadoPelaFk()
    {
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CursoForaDeSede, Guid.CreateVersion7(), "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.LocaisOferta.Add(local);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException));
    }

    [Fact(DisplayName = "CA-05: remover LocalOferta referenciado por oferta de curso viva é bloqueado; oferta removida libera (#731)")]
    public async Task RemoverLocalOferta_ComOfertaCursoViva_Bloqueia()
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), "Engenharia Civil", "Bacharelado", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.Add(curso);
            ctx.LocaisOferta.Add(local);
            await ctx.SaveChangesAsync();
        }

        UnidadeOfertante unidade = UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;
        OfertaCurso oferta = OfertaCurso.Criar(
            curso.Id, local.Id, unidade, "REGULAR", "PRESENCIAL",
            null, null, null, null, null, null).Value!;
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.OfertasCurso.Add(oferta);
            await ctx.SaveChangesAsync();
        }

        // Oferta viva referenciando o local → RemoverLocalOfertaCommandHandler bloqueia.
        await using (ConfiguracaoDbContext handlerCtx = _fixture.CreateDbContext(AdminB))
        {
            var repository = new LocalOfertaRepository(handlerCtx);

            Result resultado = await RemoverLocalOfertaCommandHandler.Handle(
                new RemoverLocalOfertaCommand(local.Id), repository, handlerCtx, CancellationToken.None);

            resultado.IsFailure.Should().BeTrue();
            resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.RemocaoBloqueadaPorOfertaCurso);
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            (await ctx.LocaisOferta.AnyAsync(l => l.Id == local.Id)).Should().BeTrue(
                "o local permanece vivo — a remoção foi bloqueada, não executada");
        }

        // Soft-delete da oferta: o local deixa de estar referenciado por oferta viva.
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            OfertaCurso ofertaTracked = await ctx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);
            ctx.OfertasCurso.Remove(ofertaTracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext handlerCtx = _fixture.CreateDbContext(AdminB))
        {
            var repository = new LocalOfertaRepository(handlerCtx);

            Result resultado = await RemoverLocalOfertaCommandHandler.Handle(
                new RemoverLocalOfertaCommand(local.Id), repository, handlerCtx, CancellationToken.None);

            resultado.IsSuccess.Should().BeTrue("oferta morta não bloqueia — o local fica livre para remoção");
        }
    }

    private static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
