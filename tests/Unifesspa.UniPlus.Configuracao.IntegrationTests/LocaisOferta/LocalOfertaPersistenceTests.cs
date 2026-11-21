namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.LocaisOferta;

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
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

[Collection(ConfiguracaoDbCollection.Name)]
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
        Campus campus = await PersistirCampusAsync();
        LocalOferta local = NovoLocal(TipoLocalOferta.PoloEad, campus.Id, null, "55555");
        await PersistirAsync(local);

        LocalOferta persistido = await LerAsync(local.Id);

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
        LocalOferta local = NovoLocal(TipoLocalOferta.ConvenioInteriorizacao, null, null, null);
        await PersistirAsync(local);

        LocalOferta persistido = await LerAsync(local.Id);

        persistido.CampusResponsavelId.Should().BeNull();
    }

    [Fact(DisplayName = "CA-01/CA-07: criar LocalOferta com endereço estruturado faz round-trip das colunas owned")]
    public async Task Insert_ComEndereco_RoundTrip()
    {
        LocalOferta local = NovoLocal(TipoLocalOferta.PoloEad, null, EnderecoDeMaraba(), null);
        await PersistirAsync(local);

        LocalOferta persistido = await LerAsync(local.Id);

        persistido.Endereco.Should().NotBeNull();
        persistido.Endereco?.Cep.Should().Be("68507590");
        persistido.Endereco?.CidadeCodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "FK campus_responsavel_id inexistente é rejeitada pelo banco")]
    public async Task Insert_CampusResponsavelInexistente_RejeitadoPelaFk()
    {
        LocalOferta local = LocalOferta.Criar(
            new(TipoLocalOferta.CursoForaDeSede, Guid.CreateVersion7(), "1504208", "Marabá", "PA", null, null),
            Agora).ValorEsperado();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.LocaisOferta.Add(local);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException));
    }

    [Fact(DisplayName = "CA-05: remover LocalOferta referenciado por oferta de curso viva é bloqueado; oferta removida libera (#731)")]
    public async Task RemoverLocalOferta_ComOfertaCursoViva_Bloqueia()
    {
        LocalOferta local = NovoLocal(TipoLocalOferta.CampusSede, null, null, null);
        OfertaCurso oferta = await PersistirOfertaNoLocalAsync(local);

        await ConferirRemocaoBloqueadaAsync(local.Id);

        await RemoverOfertaAsync(oferta.Id);

        Result liberada = await RemoverLocalAsync(local.Id);

        liberada.IsSuccess.Should().BeTrue("oferta morta não bloqueia — o local fica livre para remoção");
    }

    private static LocalOferta NovoLocal(
        TipoLocalOferta tipo, Guid? campusResponsavelId, ReferenciaEnderecoGeo? endereco, string? codigoExterno) =>
        LocalOferta.Criar(
            new(tipo, campusResponsavelId, "1504208", "Marabá", "PA", endereco, codigoExterno),
            Agora).ValorEsperado();

    private static ReferenciaEnderecoGeo EnderecoDeMaraba() =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            "1504208", "Marabá", "PA", -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).ValorEsperado();

    private async Task<Campus> PersistirCampusAsync()
    {
        Campus campus = Campus.Criar(
            "LOFCAM", "Campus Local", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).ValorEsperado();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Campi.Add(campus);
        await ctx.SaveChangesAsync();
        return campus;
    }

    private async Task PersistirAsync(LocalOferta local)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.LocaisOferta.Add(local);
        await ctx.SaveChangesAsync();
    }

    private async Task<LocalOferta> LerAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await ctx.LocaisOferta.SingleAsync(l => l.Id == id);
    }

    private async Task<bool> ExisteAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await ctx.LocaisOferta.AnyAsync(l => l.Id == id);
    }

    private static OfertaCurso NovaOferta(Curso curso, LocalOferta local)
    {
        UnidadeOfertante unidade = UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").ValorEsperado();
        return OfertaCurso.Criar(
            curso.Id, local.Id, unidade,
            new DeclaracaoDaOfertaDeCurso($"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"])).ValorEsperado();
    }

    private async Task ConferirRemocaoBloqueadaAsync(Guid localId)
    {
        Result bloqueada = await RemoverLocalAsync(localId);

        bloqueada.IsFailure.Should().BeTrue();
        bloqueada.Error?.Code.Should().Be(LocalOfertaErrorCodes.RemocaoBloqueadaPorOfertaCurso);
        (await ExisteAsync(localId)).Should().BeTrue("o local permanece vivo — a remoção foi bloqueada, não executada");
    }

    private async Task<OfertaCurso> PersistirOfertaNoLocalAsync(LocalOferta local)
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), "Engenharia Civil", "Bacharelado", "Graduação", null).ValorEsperado();
        OfertaCurso oferta = NovaOferta(curso, local);
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.Add(curso);
        ctx.LocaisOferta.Add(local);
        await ctx.SaveChangesAsync();
        ctx.OfertasCurso.Add(oferta);
        await ctx.SaveChangesAsync();
        return oferta;
    }

    private async Task RemoverOfertaAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        OfertaCurso rastreada = await ctx.OfertasCurso.SingleAsync(o => o.Id == id);
        ctx.OfertasCurso.Remove(rastreada);
        await ctx.SaveChangesAsync();
    }

    private async Task<Result> RemoverLocalAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        return await RemoverLocalOfertaCommandHandler.Handle(
            new RemoverLocalOfertaCommand(id), new LocalOfertaRepository(ctx), ctx, CancellationToken.None);
    }

    private static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
