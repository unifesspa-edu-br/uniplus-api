namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Persistência de Distrito/Bairro (+faixas de CEP) contra Postgres+PostGIS real
/// (story #670). Sem código IBGE: chave natural <c>(cidade_id, nome_normalizado)</c>.
/// Fixture compartilhada — cada teste semeia sua própria cidade com chaves únicas.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class DistritoBairroPersistenceTests
{
    private const string VersaoDataset = "202601";

    private readonly GeoPostgisFixture _fixture;

    public DistritoBairroPersistenceTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: Distrito e Bairro (+faixas) persistem vinculados à Cidade, com Point 4326")]
    public async Task DistritoBairro_PersisteELe()
    {
        CidadeSeed seed = NovaCidade();
        Cidade cidade = seed.Cidade;

        Distrito distrito = GeoTestKeys.Ok(Distrito.Importar(
            cidadeId: cidade.Id, uf: "SP", nome: "Zona Norte", nomeNormalizado: "zona norte",
            latitude: -23.45m, longitude: -46.62m,
            coordenada: new Point(-46.62, -23.45) { SRID = 4326 },
            idOrigemDne: "1001", versaoDataset: VersaoDataset));

        Bairro bairro = GeoTestKeys.Ok(Bairro.Importar(
            cidadeId: cidade.Id, uf: "SP", nome: "Sé", nomeNormalizado: "se",
            latitude: -23.55m, longitude: -46.63m,
            coordenada: new Point(-46.63, -23.55) { SRID = 4326 },
            idOrigemDne: "2002", versaoDataset: VersaoDataset));

        DistritoFaixaCep distritoFaixa = GeoTestKeys.Ok(DistritoFaixaCep.Importar(distrito.Id, "02000000", "02099999", VersaoDataset));
        BairroFaixaCep bairroFaixa = GeoTestKeys.Ok(BairroFaixaCep.Importar(bairro.Id, "01000000", "01099999", VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            seed.Adicionar(ctx);
            ctx.Distritos.Add(distrito);
            ctx.Bairros.Add(bairro);
            ctx.DistritoFaixasCep.Add(distritoFaixa);
            ctx.BairroFaixasCep.Add(bairroFaixa);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Distrito distritoLido = await leitura.Distritos.SingleAsync(d => d.Id == distrito.Id);
        Bairro bairroLido = await leitura.Bairros.SingleAsync(b => b.Id == bairro.Id);
        DistritoFaixaCep dFaixa = await leitura.DistritoFaixasCep.SingleAsync(f => f.Id == distritoFaixa.Id);
        BairroFaixaCep bFaixa = await leitura.BairroFaixasCep.SingleAsync(f => f.Id == bairroFaixa.Id);

        distritoLido.Id.Version.Should().Be(7, "EntityBase usa Guid v7 (ADR-0032)");
        distritoLido.CidadeId.Should().Be(cidade.Id);
        distritoLido.Coordenada.Should().NotBeNull();
        distritoLido.Coordenada!.SRID.Should().Be(4326);
        distritoLido.IdOrigemDne.Should().Be("1001");
        bairroLido.CidadeId.Should().Be(cidade.Id);
        bairroLido.Coordenada!.SRID.Should().Be(4326);
        dFaixa.DistritoId.Should().Be(distrito.Id);
        bFaixa.BairroId.Should().Be(bairro.Id);
    }

    [Fact(DisplayName = "CA-02: nome duplicado na mesma cidade é rejeitado (constraint composta cidade+nome)")]
    public async Task Distrito_NomeDuplicadoMesmaCidade_Rejeita()
    {
        CidadeSeed seed = NovaCidade();
        Cidade cidade = seed.Cidade;

        Distrito d1 = GeoTestKeys.Ok(Distrito.Importar(cidade.Id, "SP", "Centro", "centro", null, null, null, "1", VersaoDataset));
        Distrito d2 = GeoTestKeys.Ok(Distrito.Importar(cidade.Id, "SP", "Centro", "centro", null, null, null, "2", VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            seed.Adicionar(ctx);
            ctx.Distritos.Add(d1);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Distritos.Add(d2);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_distrito_cidade_nome");
        }
    }

    [Fact(DisplayName = "CA-03: id_origem_dne não é identidade — só (cidade+nome) define a unicidade")]
    public async Task IdOrigemDne_NaoIdentidade()
    {
        // d1 e d2 têm o MESMO (cidade, nome_normalizado) mas id_origem_dne distinto:
        // a unicidade não considera id_origem_dne, então a segunda carga é barrada.
        CidadeSeed seed = NovaCidade();
        Cidade cidade = seed.Cidade;
        Distrito d1 = GeoTestKeys.Ok(Distrito.Importar(cidade.Id, "SP", "Vila A", "vila a", null, null, null, "9001", VersaoDataset));
        Distrito d2 = GeoTestKeys.Ok(Distrito.Importar(cidade.Id, "SP", "Vila A", "vila a", null, null, null, "9999", VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            seed.Adicionar(ctx);
            ctx.Distritos.Add(d1);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Distritos.Add(d2);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_distrito_cidade_nome");
        }
    }

    [Fact(DisplayName = "CA-02: mesmo nome em cidades distintas coexiste (bairro)")]
    public async Task Bairro_MesmoNomeCidadesDistintas_Aceita()
    {
        CidadeSeed seedX = NovaCidade();
        CidadeSeed seedY = NovaCidade();

        Bairro bX = GeoTestKeys.Ok(Bairro.Importar(seedX.Cidade.Id, "SP", "Centro", "centro", null, null, null, null, VersaoDataset));
        Bairro bY = GeoTestKeys.Ok(Bairro.Importar(seedY.Cidade.Id, "RJ", "Centro", "centro", null, null, null, null, VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            seedX.Adicionar(ctx);
            seedY.Adicionar(ctx);
            ctx.Bairros.Add(bX);
            ctx.Bairros.Add(bY);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        int coexistentes = await leitura.Bairros.CountAsync(b => b.Id == bX.Id || b.Id == bY.Id);
        coexistentes.Should().Be(2, "a chave é (cidade+nome); cidades distintas comportam o mesmo bairro");
    }

    // Agregado de seed (pais → estado → cidade) com chaves naturais únicas.
    private static CidadeSeed NovaCidade()
    {
        Pais pais = GeoTestKeys.Ok(Pais.Importar(GeoTestKeys.SiglaIso(), "BR", "Brasil", null, null, null, null, VersaoDataset));
        Estado estado = GeoTestKeys.Ok(Estado.Importar(pais.Id, GeoTestKeys.Uf(), "Estado seed", null, null, null, null, null, null, null, null, null, VersaoDataset));
        Cidade cidade = GeoTestKeys.Ok(Cidade.Importar(
            estado.Id, "SP", GeoTestKeys.CodigoIbge(), "Cidade seed", "cidade seed", null,
            null, null, null, null, null, null, null, null, null, null, null, VersaoDataset));
        return new CidadeSeed(pais, estado, cidade);
    }

    private sealed record CidadeSeed(Pais Pais, Estado Estado, Cidade Cidade)
    {
        public void Adicionar(GeoDbContext ctx)
        {
            ctx.Paises.Add(Pais);
            ctx.Estados.Add(Estado);
            ctx.Cidades.Add(Cidade);
        }
    }
}
