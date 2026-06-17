namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Persistência de País/Estado/EstadoIndicador/EstadoFaixaCep contra Postgres+PostGIS
/// real (story #668). O <see cref="GeoPostgisFixture"/> é compartilhado pela coleção:
/// cada teste gera chaves naturais únicas (<see cref="GeoTestKeys"/>) para não colidir
/// nos índices UNIQUE — exceto o teste de duplicata, cuja colisão é o objeto.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class PaisEstadoPersistenceTests
{
    private const string VersaoDataset = "202601";

    private readonly GeoPostgisFixture _fixture;

    public PaisEstadoPersistenceTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: País/Estado/Indicador(1:1)/Faixa persistem e relêem com Id Guid v7 e vínculos")]
    public async Task PaisEstado_PersisteELe()
    {
        Pais brasil = GeoTestKeys.Ok(Pais.Importar(
            siglaIso: GeoTestKeys.SiglaIso(), sigla: "BR", nome: "Brasil",
            codigoBcb: "1058", codigoRfb: "105", codigoSped: null, codigoSiscomex: null,
            versaoDataset: VersaoDataset));

        Estado para = GeoTestKeys.Ok(Estado.Importar(
            paisId: brasil.Id, uf: GeoTestKeys.Uf(), nome: "Pará", nomeNormalizado: "para",
            regiao: "Norte", capital: "Belém", codigoIbge: "15",
            latitude: -1.45502m, longitude: -48.5024m,
            coordenada: new Point(-48.5024, -1.45502) { SRID = 4326 },
            cepInicial: "66000000", cepFinal: "68899999",
            versaoDataset: VersaoDataset));

        EstadoIndicador indicador = GeoTestKeys.Ok(EstadoIndicador.Importar(
            estadoId: para.Id, gentilico: "paraense", governador: null,
            areaKm2: 1245870.704m, populacaoResidente2022: 8121025, densidadeDemografica: 6.52m,
            matriculasEnsinoFundamental2023: null, idh: 0.690m,
            receitasBrutas: 30_500_000_000.55m, despesasBrutas: 28_000_000_000.42m,
            rendimentoMensalPerCapita: null, totalVeiculos2023: 1_900_000,
            versaoDataset: VersaoDataset));

        EstadoFaixaCep faixa = GeoTestKeys.Ok(EstadoFaixaCep.Importar(
            estadoId: para.Id, cepInicial: "66000000", cepFinal: "66999999",
            descricao: "Capital: Belém", versaoDataset: VersaoDataset));

        await using (GeoDbContext gravacao = _fixture.CreateDbContext())
        {
            gravacao.Paises.Add(brasil);
            gravacao.Estados.Add(para);
            gravacao.EstadoIndicadores.Add(indicador);
            gravacao.EstadoFaixasCep.Add(faixa);
            await gravacao.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Estado estadoLido = await leitura.Estados.SingleAsync(e => e.Id == para.Id);
        Pais paisLido = await leitura.Paises.SingleAsync(p => p.Id == brasil.Id);
        EstadoIndicador indicadorLido = await leitura.EstadoIndicadores.SingleAsync(i => i.EstadoId == para.Id);
        EstadoFaixaCep faixaLida = await leitura.EstadoFaixasCep.SingleAsync(f => f.Id == faixa.Id);

        paisLido.Id.Version.Should().Be(7, "EntityBase usa Guid v7 (ADR-0032)");
        estadoLido.Id.Version.Should().Be(7);
        estadoLido.PaisId.Should().Be(brasil.Id);
        estadoLido.Nome.Should().Be("Pará");
        estadoLido.Coordenada.Should().NotBeNull();
        estadoLido.Coordenada!.SRID.Should().Be(4326);
        indicadorLido.EstadoId.Should().Be(para.Id);
        indicadorLido.ReceitasBrutas.Should().Be(30_500_000_000.55m);
        indicadorLido.PopulacaoResidente2022.Should().Be(8121025);
        faixaLida.EstadoId.Should().Be(para.Id);
        faixaLida.Descricao.Should().Be("Capital: Belém");
    }

    [Fact(DisplayName = "CA-02: UF e sigla_iso duplicadas são rejeitadas pela constraint única correta")]
    public async Task ChaveNatural_RejeitaDuplicata()
    {
        // UF duplicada — verifica que falhou no ix_estado_uf, não em outra constraint.
        Pais paisUf = GeoTestKeys.Ok(Pais.Importar(GeoTestKeys.SiglaIso(), "CL", "Chile", null, null, null, null, VersaoDataset));
        string uf = GeoTestKeys.Uf();
        Estado uf1 = NovoEstado(paisUf.Id, uf, "Estado A");
        Estado uf2 = NovoEstado(paisUf.Id, uf, "Estado A duplicata");

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(paisUf);
            ctx.Estados.Add(uf1);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Estados.Add(uf2);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_estado_uf");
        }

        // sigla_iso duplicada.
        string siglaIso = GeoTestKeys.SiglaIso();
        Pais bol1 = GeoTestKeys.Ok(Pais.Importar(siglaIso, "BO", "Bolívia", null, null, null, null, VersaoDataset));
        Pais bol2 = GeoTestKeys.Ok(Pais.Importar(siglaIso, "BO", "Bolívia duplicata", null, null, null, null, VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(bol1);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(bol2);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_pais_sigla_iso");
        }
    }

    [Fact(DisplayName = "CA-02: chave natural é case-insensitive por normalização (bra==BRA, pa==PA)")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "O teste cria deliberadamente a variante minúscula da sigla para provar que a factory a normaliza para maiúsculas.")]
    public async Task ChaveNatural_NormalizaCaixa()
    {
        string siglaBase = GeoTestKeys.SiglaIso();
        Pais maiusculo = GeoTestKeys.Ok(Pais.Importar(siglaBase.ToUpperInvariant(), "PY", "Paraguai", null, null, null, null, VersaoDataset));
        Pais minusculo = GeoTestKeys.Ok(Pais.Importar(siglaBase.ToLowerInvariant(), "PY", "Paraguai (caixa baixa)", null, null, null, null, VersaoDataset));

        maiusculo.SiglaIso.Should().Be(minusculo.SiglaIso, "a factory normaliza a sigla para maiúsculas");

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(maiusculo);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(minusculo);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_pais_sigla_iso");
        }
    }

    [Fact(DisplayName = "CA-03: Coordenada Point SRID 4326 persiste e relê via NetTopologySuite (geography)")]
    public async Task Estado_Coordenada_Point4326()
    {
        const double longitude = -49.1278;
        const double latitude = -5.3686;

        Pais pais = GeoTestKeys.Ok(Pais.Importar(GeoTestKeys.SiglaIso(), "AR", "Argentina", null, null, null, null, VersaoDataset));
        Estado estado = GeoTestKeys.Ok(Estado.Importar(
            pais.Id, GeoTestKeys.Uf(), "São Paulo", null, null, null, null,
            (decimal)latitude, (decimal)longitude,
            new Point(longitude, latitude) { SRID = 4326 },
            null, null, VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(pais);
            ctx.Estados.Add(estado);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Estado lido = await leitura.Estados.SingleAsync(e => e.Id == estado.Id);

        lido.Coordenada.Should().NotBeNull();
        lido.Coordenada!.X.Should().BeApproximately(longitude, 1e-6);
        lido.Coordenada.Y.Should().BeApproximately(latitude, 1e-6);
        lido.Coordenada.SRID.Should().Be(4326);
    }

    [Fact(DisplayName = "CA-03: EstadoIndicador com dado ausente persiste null sem erro")]
    public async Task EstadoIndicador_DadoAusente_Null()
    {
        Pais pais = GeoTestKeys.Ok(Pais.Importar(GeoTestKeys.SiglaIso(), "UY", "Uruguai", null, null, null, null, VersaoDataset));
        Estado estado = NovoEstado(pais.Id, GeoTestKeys.Uf(), "Rio de Janeiro");
        EstadoIndicador indicador = GeoTestKeys.Ok(EstadoIndicador.Importar(
            estado.Id, gentilico: null, governador: null,
            areaKm2: null, populacaoResidente2022: null, densidadeDemografica: null,
            matriculasEnsinoFundamental2023: null, idh: null,
            receitasBrutas: null, despesasBrutas: null,
            rendimentoMensalPerCapita: null, totalVeiculos2023: null,
            versaoDataset: VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(pais);
            ctx.Estados.Add(estado);
            ctx.EstadoIndicadores.Add(indicador);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        EstadoIndicador lido = await leitura.EstadoIndicadores.SingleAsync(i => i.EstadoId == estado.Id);

        lido.Idh.Should().BeNull();
        lido.ReceitasBrutas.Should().BeNull();
        lido.PopulacaoResidente2022.Should().BeNull();
        lido.Vigente.Should().BeTrue();
        lido.VersaoDataset.Should().Be(VersaoDataset);
    }

    [Fact(DisplayName = "CA-05: vigente=false persiste de fato (não é mascarado pelo default DDL true)")]
    public async Task Vigente_False_Persiste()
    {
        Pais pais = GeoTestKeys.Ok(Pais.Importar(GeoTestKeys.SiglaIso(), "PE", "Peru", null, null, null, null, VersaoDataset, vigente: false));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Paises.Add(pais);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Pais lido = await leitura.Paises.SingleAsync(p => p.Id == pais.Id);

        lido.Vigente.Should().BeFalse(
            "a marcação de stale (vigente=false) precisa chegar ao banco — não pode ser substituída pelo default true");
    }

    private static Estado NovoEstado(Guid paisId, string uf, string nome) =>
        GeoTestKeys.Ok(Estado.Importar(paisId, uf, nome, null, null, null, null, null, null, null, null, null, VersaoDataset));
}
