namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Persistência de Cidade (territorial embutido) + CidadeIndicador (1:1) +
/// CidadeFaixaCep contra Postgres+PostGIS real (story #669). Fixture compartilhada:
/// chaves naturais únicas por teste (<see cref="GeoTestKeys"/>), exceto o teste de
/// duplicata.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class CidadePersistenceTests
{
    private const string VersaoDataset = "202601";

    private readonly GeoPostgisFixture _fixture;

    public CidadePersistenceTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: Cidade (territorial) + Indicador(1:1) + Faixa persistem e relêem com Point 4326")]
    public async Task Cidade_PersisteELe()
    {
        (Pais pais, Estado estado) = await SemearPaisEstadoAsync();

        Cidade maraba = GeoTestKeys.Ok(Cidade.Importar(
            estadoId: estado.Id, uf: "PA", codigoIbge: GeoTestKeys.CodigoIbge(), nome: "Marabá",
            nomeNormalizado: "maraba", ddd: "94",
            latitude: -5.36867m, longitude: -49.11731m,
            coordenada: new Point(-49.11731, -5.36867) { SRID = 4326 },
            mesorregiaoCodigo: "1503", mesorregiaoNome: "Sudeste Paraense",
            microrregiaoCodigo: "15009", microrregiaoNome: "Marabá",
            regiaoIntermediariaCodigo: "1503", regiaoIntermediariaNome: "Marabá",
            regiaoImediataCodigo: "150009", regiaoImediataNome: "Marabá",
            versaoDataset: VersaoDataset));

        CidadeIndicador indicador = GeoTestKeys.Ok(CidadeIndicador.Importar(
            cidadeId: maraba.Id, gentilico: "marabaense", prefeito: null,
            areaKm2: 15128.058m, populacaoResidente: 233669, densidadeDemografica: 15.45m,
            escolarizacao6a14: 96.8m, idh: 0.668m, mortalidadeInfantil: 17.2m,
            receitas: 1_200_000_000.00m, despesas: 1_100_000_000.00m, pibPerCapita: 28000.50m,
            aniversario: "05/04", versaoDataset: VersaoDataset));

        CidadeFaixaCep faixa = GeoTestKeys.Ok(CidadeFaixaCep.Importar(
            cidadeId: maraba.Id, cepInicial: "68500000", cepFinal: "68519999",
            versaoDataset: VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(pais);
            ctx.Estados.Add(estado);
            ctx.Cidades.Add(maraba);
            ctx.CidadeIndicadores.Add(indicador);
            ctx.CidadeFaixasCep.Add(faixa);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Cidade lida = await leitura.Cidades.SingleAsync(c => c.Id == maraba.Id);
        CidadeIndicador indicadorLido = await leitura.CidadeIndicadores.SingleAsync(i => i.CidadeId == maraba.Id);
        CidadeFaixaCep faixaLida = await leitura.CidadeFaixasCep.SingleAsync(f => f.Id == faixa.Id);

        lida.Id.Version.Should().Be(7, "EntityBase usa Guid v7 (ADR-0032)");
        lida.EstadoId.Should().Be(estado.Id);
        lida.MesorregiaoNome.Should().Be("Sudeste Paraense");
        lida.RegiaoImediataNome.Should().Be("Marabá");
        lida.Coordenada.Should().NotBeNull();
        lida.Coordenada!.SRID.Should().Be(4326);
        indicadorLido.Aniversario.Should().Be("05/04");
        indicadorLido.MortalidadeInfantil.Should().Be(17.2m);
        faixaLida.CidadeId.Should().Be(maraba.Id);
    }

    [Fact(DisplayName = "CA-02: código IBGE duplicado é rejeitado pela constraint única correta")]
    public async Task Cidade_CodigoIbgeDuplicado_Rejeita()
    {
        (Pais pais, Estado estado) = await SemearPaisEstadoAsync();
        string codigoIbge = GeoTestKeys.CodigoIbge();

        Cidade c1 = NovaCidade(estado.Id, "PA", codigoIbge, "Cidade A");
        Cidade c2 = NovaCidade(estado.Id, "PA", codigoIbge, "Cidade A duplicata");

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(pais);
            ctx.Estados.Add(estado);
            ctx.Cidades.Add(c1);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Cidades.Add(c2);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_cidade_codigo_ibge");
        }
    }

    [Fact(DisplayName = "CA-02: homônimos em UFs distintas coexistem (chave é o código IBGE, não o nome)")]
    public async Task Cidade_HomonimosUfDistintas_Aceita()
    {
        (Pais pais, Estado pi) = await SemearPaisEstadoAsync();
        Estado rs = GeoTestKeys.Ok(Estado.Importar(pais.Id, GeoTestKeys.Uf(), "Rio Grande do Sul", null, null, null, null, null, null, null, null, null, VersaoDataset));

        Cidade bomJesusPi = NovaCidade(pi.Id, "PI", GeoTestKeys.CodigoIbge(), "Bom Jesus");
        Cidade bomJesusRs = NovaCidade(rs.Id, "RS", GeoTestKeys.CodigoIbge(), "Bom Jesus");

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(pais);
            ctx.Estados.Add(pi);
            ctx.Estados.Add(rs);
            ctx.Cidades.Add(bomJesusPi);
            ctx.Cidades.Add(bomJesusRs);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        int homonimos = await leitura.Cidades.CountAsync(c => c.Nome == "Bom Jesus"
            && (c.Id == bomJesusPi.Id || c.Id == bomJesusRs.Id));
        homonimos.Should().Be(2);
    }

    [Fact(DisplayName = "CA-03: CidadeIndicador com mortalidade ausente persiste null sem erro")]
    public async Task CidadeIndicador_DadoAusente_Null()
    {
        (Pais pais, Estado estado) = await SemearPaisEstadoAsync();
        Cidade cidade = NovaCidade(estado.Id, "PA", GeoTestKeys.CodigoIbge(), "Parauapebas");
        CidadeIndicador indicador = GeoTestKeys.Ok(CidadeIndicador.Importar(
            cidadeId: cidade.Id, gentilico: null, prefeito: null,
            areaKm2: null, populacaoResidente: null, densidadeDemografica: null,
            escolarizacao6a14: null, idh: null, mortalidadeInfantil: null,
            receitas: null, despesas: null, pibPerCapita: null,
            aniversario: null, versaoDataset: VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(pais);
            ctx.Estados.Add(estado);
            ctx.Cidades.Add(cidade);
            ctx.CidadeIndicadores.Add(indicador);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        CidadeIndicador lido = await leitura.CidadeIndicadores.SingleAsync(i => i.CidadeId == cidade.Id);
        lido.MortalidadeInfantil.Should().BeNull();
        lido.Idh.Should().BeNull();
    }

    private static Task<(Pais Pais, Estado Estado)> SemearPaisEstadoAsync()
    {
        Pais pais = GeoTestKeys.Ok(Pais.Importar(GeoTestKeys.SiglaIso(), "BR", "Brasil", null, null, null, null, VersaoDataset));
        Estado estado = GeoTestKeys.Ok(Estado.Importar(pais.Id, GeoTestKeys.Uf(), "Estado seed", null, null, null, null, null, null, null, null, null, VersaoDataset));
        return Task.FromResult((pais, estado));
    }

    private static Cidade NovaCidade(Guid estadoId, string uf, string codigoIbge, string nome) =>
        GeoTestKeys.Ok(Cidade.Importar(
            estadoId, uf, codigoIbge, nome, nome, null,
            null, null, null,
            null, null, null, null, null, null, null, null,
            VersaoDataset));
}
