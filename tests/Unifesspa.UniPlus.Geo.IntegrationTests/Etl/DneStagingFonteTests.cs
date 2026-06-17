namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="DneStagingFonte"/>: lê o dataset DNE de um schema de staging real
/// (dumps restaurados) por <c>SELECT</c> streamado, e o importador roda fim-a-fim
/// sobre ele. Cobre também a validação anti-injeção de versão/schema.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class DneStagingFonteTests
{
    private const string Versao = "202601";
    private const string CodigoMaraba = "1500402";

    private readonly GeoPostgisFixture _fixture;

    public DneStagingFonteTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Lê os registros do staging mapeando colunas para os records crus")]
    public async Task LeRegistros_MapeiaColunas()
    {
        await CriarStagingAsync();
        DneStagingFonte fonte = new(_fixture.ConnectionString, Versao);

        List<PaisCru> paises = await ColetarAsync(fonte.LerPaisesAsync);
        paises.Should().HaveCount(2);
        paises.Should().Contain(p => p.SiglaIso == "BRA" && p.Nome == "Brasil");

        List<EstadoCru> estados = await ColetarAsync(fonte.LerEstadosAsync);
        estados.Should().ContainSingle(e => e.Uf == "PA" && e.Latitude == "-1.9981271");

        List<CidadeCru> cidades = await ColetarAsync(fonte.LerCidadesAsync);
        cidades.Should().ContainSingle(c => c.CodigoIbge == CodigoMaraba && c.Uf == "PA");
    }

    [Fact(DisplayName = "Importador roda fim-a-fim sobre o staging (SELECT streamado → upsert), com parse tolerante")]
    public async Task EndToEnd_PopulaBancoViaStaging()
    {
        await LimparDominioAsync();
        await CriarStagingAsync();

        DneStagingFonte fonte = new(_fixture.ConnectionString, Versao);
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            GeoImportadorPaisEstadoCidade importador = new(ctx, NullLogger<GeoImportadorPaisEstadoCidade>.Instance);
            await importador.ImportarAsync(fonte, CancellationToken.None);
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Paises.CountAsync()).Should().Be(1);
        Estado para = await leitura.Estados.SingleAsync(e => e.Uf == "PA");
        para.CodigoIbge.Should().Be("15");

        Cidade maraba = await leitura.Cidades.SingleAsync(c => c.CodigoIbge == CodigoMaraba);
        maraba.MesorregiaoNome.Should().Be("Sudeste Paraense");
        maraba.Coordenada.Should().NotBeNull();

        CidadeIndicador indicador = await leitura.CidadeIndicadores.SingleAsync(i => i.CidadeId == maraba.Id);
        indicador.MortalidadeInfantil.Should().BeNull(); // '-' no staging degradou para null
    }

    [Theory(DisplayName = "Versão fora do formato AAAAMM é rejeitada no construtor (anti-injeção)")]
    [InlineData("abc")]
    [InlineData("2026")]
    [InlineData("20260")]
    [InlineData("2026-01")]
    public void VersaoInvalida_Lanca(string versao)
    {
        Action criar = () => _ = new DneStagingFonte(_fixture.ConnectionString, versao);
        criar.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Nome de schema não-identificador é rejeitado no construtor (anti-injeção)")]
    [InlineData("dne staging")]
    [InlineData("dne;drop")]
    [InlineData("1schema")]
    public void SchemaInvalido_Lanca(string schema)
    {
        Action criar = () => _ = new DneStagingFonte(_fixture.ConnectionString, Versao, schema);
        criar.Should().Throw<ArgumentException>();
    }

    private static async Task<List<T>> ColetarAsync<T>(Func<CancellationToken, IAsyncEnumerable<T>> ler)
    {
        List<T> itens = [];
        await foreach (T item in ler(CancellationToken.None).ConfigureAwait(false))
        {
            itens.Add(item);
        }

        return itens;
    }

    private async Task LimparDominioAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE TABLE pais CASCADE");
    }

    private async Task CriarStagingAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync(StagingSql);
    }

    // Schema de staging reduzido: só as colunas que a DneStagingFonte projeta, com
    // dados mínimos consistentes (Brasil + Pará + Marabá; mortalidade '-' para o parse
    // tolerante). Recriado a cada teste (DROP CASCADE) para isolamento.
    private const string StagingSql = """
        DROP SCHEMA IF EXISTS dne_staging CASCADE;
        CREATE SCHEMA dne_staging;

        CREATE TABLE dne_staging."tbl_cep_202601_n_paises_ibge" (
            sigla_iso text, sigla text, nome_pt text, pais_bcb text, pais_rbf text, pais_sped text, pais_siscomex text);
        INSERT INTO dne_staging."tbl_cep_202601_n_paises_ibge" VALUES
            ('BRA','BR','Brasil','1058','106','1058','105'),
            ('ARG','AR','Argentina','639','63','639','63');

        CREATE TABLE dne_staging."tbl_cep_202601_n_estado" (
            sigla text, estado text, estado_sem_acento text, regiao text, capital text,
            faixa_ini text, faixa_fim text, latitude text, longitude text);
        INSERT INTO dne_staging."tbl_cep_202601_n_estado" VALUES
            ('PA','Pará','Para','Norte','Belém','66000000','68899999','-1.9981271','-54.9306152');

        CREATE TABLE dne_staging."tbl_cep_202601_n_estado_ibge" (
            uf text, codigo_ibge text, gentilico text, governador text, area_territorial_km2 text,
            populacao_residente_2022 text, densidade_demografica_hab_km2 text, matriculas_ensino_fundamental_2023 text,
            idh_indice_desenv_humano text, total_receitas_brutas_realizadas text, total_despesas_brutas_empenhadas text,
            rendimento_mensal_per_capita text, total_veiculos_2023 text);
        INSERT INTO dne_staging."tbl_cep_202601_n_estado_ibge" VALUES
            ('PA','15','paraense','HELDER','1245870.242','8120131','6.52','1331723','0.69','52747856526.44','43981234831.53','1282','2627090');

        CREATE TABLE dne_staging."tbl_cep_202601_n_estado_faixa" (
            sigla text, regiao text, faixa_ini text, faixa_fim text);
        INSERT INTO dne_staging."tbl_cep_202601_n_estado_faixa" VALUES ('PA','Pará','66000000','68899999');

        CREATE TABLE dne_staging."tbl_cep_202601_n_cidade" (
            cidade_ibge text, cidade text, cidade_sem_acento text, estado text, ddd text, latitude text, longitude text);
        INSERT INTO dne_staging."tbl_cep_202601_n_cidade" VALUES
            ('1500402','Marabá','Maraba','PA','94','-5.36867','-49.11731');

        CREATE TABLE dne_staging."tbl_cep_202601_n_cidade_ibge_territorio" (
            cidade_ibge text, mesorregiao text, mesorregiao_nome text, microrregiao text, microrregiao_nome text,
            regiao_intermediaria text, regiao_intermediaria_nome text,
            regiao_geografica_imediata text, regiao_geografica_imediata_nome text);
        INSERT INTO dne_staging."tbl_cep_202601_n_cidade_ibge_territorio" VALUES
            ('1500402','1503','Sudeste Paraense','15009','Marabá','1503','Marabá','150009','Marabá');

        CREATE TABLE dne_staging."tbl_cep_202601_n_cidade_ibge" (
            cidade_ibge text, gentilico text, prefeito text, area_territorial_km2 text, populacao_residente text,
            densidade_demografica text, escolarizacao_6_a_14_anos text, idice_de_desenv_humano text,
            mortalidade_infantil text, receitas_realizadas text, despesas_empenhadas text, pib_per_capita text,
            aniversario_municipio text);
        INSERT INTO dne_staging."tbl_cep_202601_n_cidade_ibge" VALUES
            ('1500402','marabaense','PREFEITO','15128.27','283542','18.7','96.5','0.668','-','1234567.89','1000000.00','25000.00','05/04');

        CREATE TABLE dne_staging."tbl_cep_202601_n_cidade_faixa" (
            cidade_ibge text, faixa_ini text, faixa_fim text);
        INSERT INTO dne_staging."tbl_cep_202601_n_cidade_faixa" VALUES ('1500402','68500000','68519999');
        """;
}
