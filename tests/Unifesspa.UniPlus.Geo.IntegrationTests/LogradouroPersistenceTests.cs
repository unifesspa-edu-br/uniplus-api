namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Persistência de Logradouro + LogradouroComplemento + CepGrandeUsuario contra
/// Postgres+PostGIS real (story #671). Foco: cep indexado NÃO único (chave de upsert
/// composta), complemento por CEP sem FK a logradouro, grande usuário sem cidade,
/// Ativo bool. Fixture compartilhada — chaves únicas por teste (<see cref="GeoTestKeys"/>).
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class LogradouroPersistenceTests
{
    private const string VersaoDataset = "202601";

    private readonly GeoPostgisFixture _fixture;

    public LogradouroPersistenceTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: Logradouro (cidade/bairro) + Complemento + GrandeUsuario persistem com Point 4326 e Ativo")]
    public async Task Logradouro_PersisteELe()
    {
        CidadeSeed seed = NovaCidade();
        Bairro bairro = GeoTestKeys.Ok(Bairro.Importar(seed.Cidade.Id, "SP", "Sé", "se", null, null, null, null, VersaoDataset));

        string cep = GeoTestKeys.Cep();
        Logradouro logradouro = GeoTestKeys.Ok(Logradouro.Importar(
            cep: cep, tipo: "Praça", nome: "da Sé", nomeCompleto: "Praça da Sé",
            nomeNormalizado: "praca da se", cidadeId: seed.Cidade.Id, distritoId: null, bairroId: bairro.Id,
            uf: "SP", latitude: -23.55052m, longitude: -46.63331m,
            coordenada: new Point(-46.63331, -23.55052) { SRID = 4326 },
            ativo: true, versaoDataset: VersaoDataset));

        LogradouroComplemento complemento = GeoTestKeys.Ok(LogradouroComplemento.Importar(
            cep, "lado par", "lado par", VersaoDataset));

        CepGrandeUsuario grandeUsuario = GeoTestKeys.Ok(CepGrandeUsuario.Importar(
            GeoTestKeys.Cep(), "UNESP", "unesp", VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            seed.Adicionar(ctx);
            ctx.Bairros.Add(bairro);
            ctx.Logradouros.Add(logradouro);
            ctx.LogradouroComplementos.Add(complemento);
            ctx.CepGrandesUsuarios.Add(grandeUsuario);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Logradouro lido = await leitura.Logradouros.SingleAsync(l => l.Id == logradouro.Id);

        lido.Id.Version.Should().Be(7, "EntityBase usa Guid v7 (ADR-0032)");
        lido.CidadeId.Should().Be(seed.Cidade.Id);
        lido.BairroId.Should().Be(bairro.Id);
        lido.DistritoId.Should().BeNull();
        lido.Ativo.Should().BeTrue();
        lido.Coordenada.Should().NotBeNull();
        lido.Coordenada!.SRID.Should().Be(4326);
        (await leitura.CepGrandesUsuarios.SingleAsync(g => g.Id == grandeUsuario.Id)).Nome.Should().Be("UNESP");
    }

    [Fact(DisplayName = "CA-02: cep compartilhado por vários logradouros coexiste; (cep+nome+cidade) é a chave de upsert")]
    public async Task Logradouro_CepCompartilhado_Upsert()
    {
        CidadeSeed seed = NovaCidade();
        string cepGeral = GeoTestKeys.Cep();

        // Dois logradouros distintos com o MESMO cep geral — coexistem (cep não é único).
        Logradouro l1 = NovoLogradouro(cepGeral, "Rua A", "rua a", seed.Cidade.Id);
        Logradouro l2 = NovoLogradouro(cepGeral, "Rua B", "rua b", seed.Cidade.Id);
        // Mesma chave de upsert que l1 (cep+nome+cidade) — deve ser barrada.
        Logradouro l1Dup = NovoLogradouro(cepGeral, "Rua A", "rua a", seed.Cidade.Id);

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            seed.Adicionar(ctx);
            ctx.Logradouros.Add(l1);
            ctx.Logradouros.Add(l2);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext leitura = _fixture.CreateDbContext())
        {
            int compartilham = await leitura.Logradouros.CountAsync(l => l.Cep == cepGeral);
            compartilham.Should().Be(2, "cep é indexado, não único — vários logradouros compartilham o CEP geral");
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.Logradouros.Add(l1Dup);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_logradouro_natural");
        }
    }

    [Fact(DisplayName = "CA-03: complemento é por (cep, complemento_normalizado), sem FK a logradouro")]
    public async Task LogradouroComplemento_PorCep_SemFkLogradouro()
    {
        // CEP sem nenhum logradouro cadastrado — o complemento persiste mesmo assim
        // (não há FK a logradouro). Dois complementos no mesmo CEP coexistem.
        string cep = GeoTestKeys.Cep();
        LogradouroComplemento par = GeoTestKeys.Ok(LogradouroComplemento.Importar(cep, "lado par", "lado par", VersaoDataset));
        LogradouroComplemento impar = GeoTestKeys.Ok(LogradouroComplemento.Importar(cep, "lado ímpar", "lado impar", VersaoDataset));
        LogradouroComplemento parDup = GeoTestKeys.Ok(LogradouroComplemento.Importar(cep, "lado par", "lado par", VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.LogradouroComplementos.Add(par);
            ctx.LogradouroComplementos.Add(impar);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext leitura = _fixture.CreateDbContext())
        {
            int doCep = await leitura.LogradouroComplementos.CountAsync(c => c.Cep == cep);
            doCep.Should().Be(2, "o mesmo CEP comporta vários complementos, sem vínculo a um logradouro");
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.LogradouroComplementos.Add(parDup);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ux_logradouro_complemento_cep_complemento");
        }
    }

    [Fact(DisplayName = "CA-01: Ativo bool persiste (true e false)")]
    public async Task Logradouro_Ativo_BoolFromSN()
    {
        CidadeSeed seed = NovaCidade();
        Logradouro ativo = NovoLogradouro(GeoTestKeys.Cep(), "Rua Ativa", "rua ativa", seed.Cidade.Id, ativo: true);
        Logradouro inativo = NovoLogradouro(GeoTestKeys.Cep(), "Rua Inativa", "rua inativa", seed.Cidade.Id, ativo: false);

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            seed.Adicionar(ctx);
            ctx.Logradouros.Add(ativo);
            ctx.Logradouros.Add(inativo);
            await ctx.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Logradouros.SingleAsync(l => l.Id == ativo.Id)).Ativo.Should().BeTrue();
        (await leitura.Logradouros.SingleAsync(l => l.Id == inativo.Id)).Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-04: CepGrandeUsuario.cep é único")]
    public async Task CepGrandeUsuario_CepUnico()
    {
        string cep = GeoTestKeys.Cep();
        CepGrandeUsuario g1 = GeoTestKeys.Ok(CepGrandeUsuario.Importar(cep, "Órgão A", null, VersaoDataset));
        CepGrandeUsuario g2 = GeoTestKeys.Ok(CepGrandeUsuario.Importar(cep, "Órgão B", null, VersaoDataset));

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.CepGrandesUsuarios.Add(g1);
            await ctx.SaveChangesAsync();
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            ctx.CepGrandesUsuarios.Add(g2);
            Exception excecao = (await ((Func<Task>)(() => ctx.SaveChangesAsync())).Should().ThrowAsync<DbUpdateException>()).Which;
            GeoTestKeys.DeveSerViolacaoUnique(excecao, "ix_cep_grande_usuario_cep");
        }
    }

    private static Logradouro NovoLogradouro(string cep, string nome, string nomeNormalizado, Guid cidadeId, bool ativo = true) =>
        GeoTestKeys.Ok(Logradouro.Importar(
            cep, null, nome, nome, nomeNormalizado, cidadeId, null, null, "SP",
            null, null, null, ativo, VersaoDataset));

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
