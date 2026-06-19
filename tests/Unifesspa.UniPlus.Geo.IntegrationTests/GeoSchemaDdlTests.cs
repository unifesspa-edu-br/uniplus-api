namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using System.Data;
using System.Data.Common;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Verifica o DDL físico que a persistência de entidades sozinha não prova: o
/// método do índice espacial (GIST) realmente chegou ao banco. Persistir um
/// <c>Point</c> passaria mesmo sem índice; este teste inspeciona
/// <c>pg_indexes.indexdef</c> diretamente (achado de revisão).
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoSchemaDdlTests
{
    private readonly GeoPostgisFixture _fixture;

    public GeoSchemaDdlTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ix_estado_coordenada é um índice GIST sobre a coluna geográfica")]
    public async Task IndiceCoordenadaEstado_UsaGist()
    {
        string? indexDef = await ObterIndexDefAsync("ix_estado_coordenada");

        indexDef.Should().NotBeNull("o índice espacial de estado.coordenada deve existir");
        indexDef!.Should().Contain("USING gist", "a coordenada precisa de índice GIST (ADR-0091)");
    }

    [Fact(DisplayName = "ix_cidade_coordenada é GIST e ix_cidade_nome_normalizado_trgm é GIN trigram")]
    public async Task IndicesCidade_GistETrgm()
    {
        string? gist = await ObterIndexDefAsync("ix_cidade_coordenada");
        gist.Should().NotBeNull();
        gist!.Should().Contain("USING gist");

        string? trgm = await ObterIndexDefAsync("ix_cidade_nome_normalizado_trgm");
        trgm.Should().NotBeNull("o índice trigram de cidade.nome_normalizado deve existir");
        trgm!.Should().Contain("USING gin", "autocomplete usa GIN");
        trgm.Should().Contain("gin_trgm_ops", "o opclass trigram precisa estar presente (extensão pg_trgm)");
    }

    [Theory(DisplayName = "índices de coordenada de Distrito/Bairro são GIST")]
    [InlineData("ix_distrito_coordenada")]
    [InlineData("ix_bairro_coordenada")]
    public async Task IndicesCoordenadaDistritoBairro_UsamGist(string indexName)
    {
        string? indexDef = await ObterIndexDefAsync(indexName);
        indexDef.Should().NotBeNull($"o índice espacial {indexName} deve existir");
        indexDef!.Should().Contain("USING gist", "a coordenada precisa de índice GIST (ADR-0091)");
    }

    [Fact(DisplayName = "Logradouro: ix_logradouro_coordenada é GIST e ix_logradouro_nome_trgm é GIN trigram")]
    public async Task IndicesLogradouro_GistETrgm()
    {
        string? gist = await ObterIndexDefAsync("ix_logradouro_coordenada");
        gist.Should().NotBeNull();
        gist!.Should().Contain("USING gist");

        string? trgm = await ObterIndexDefAsync("ix_logradouro_nome_trgm");
        trgm.Should().NotBeNull();
        trgm!.Should().Contain("USING gin");
        trgm.Should().Contain("gin_trgm_ops");
    }

    [Fact(DisplayName = "Logradouro: ix_logradouro_natural é UNIQUE e cep NÃO é único isoladamente")]
    public async Task Logradouro_CepNaoUnico_ChaveCompostaUnica()
    {
        string? natural = await ObterIndexDefAsync("ix_logradouro_natural");
        natural.Should().NotBeNull();
        natural!.Should().Contain("UNIQUE", "a chave de upsert (cep, nome_normalizado, cidade_id) é única");
        natural.Should().Contain("cep");
        natural.Should().Contain("nome_normalizado");

        // Não deve existir índice único só sobre cep (CEP geral cobre vários logradouros).
        long unicosSoCep = await ContarIndicesUnicosSomenteCepAsync();
        unicosSoCep.Should().Be(0, "cep isolado não pode ser único na tabela logradouro");
    }

    [Theory(DisplayName = "#704: índice de range das faixas de CEP é B-tree parcial sobre (cep_inicial, cep_final) WHERE vigente")]
    [InlineData("ix_cidade_faixa_cep_range")]
    [InlineData("ix_bairro_faixa_cep_range")]
    [InlineData("ix_distrito_faixa_cep_range")]
    public async Task IndicesRangeFaixaCep_ParciaisSobreCep(string indexName)
    {
        string? indexDef = await ObterIndexDefAsync(indexName);

        indexDef.Should().NotBeNull($"o índice de range {indexName} deve existir (caminho frio do lookup, #704)");
        indexDef!.Should().Contain("cep_inicial", "o range parte de cep_inicial (predicado cep_inicial <= @cep)");
        indexDef.Should().Contain("cep_final");
        // Parcial: o lookup só consulta faixas vigentes. O Postgres pode renderizar o
        // predicado como "WHERE vigente" ou "WHERE (vigente)".
        indexDef.Should().Contain("WHERE", "o índice é parcial");
        indexDef.Should().Contain("vigente", "o predicado parcial filtra vigente");
        indexDef.Should().Contain("USING btree", "o range usa B-tree, não GiST");
    }

    [Fact(DisplayName = "extensão pg_trgm está instalada (sustenta o índice trigram)")]
    public async Task ExtensaoPgTrgm_Instalada()
    {
        await using GeoDbContext context = _fixture.CreateDbContext();
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pg_extension WHERE extname = 'pg_trgm';";
        long presentes = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        presentes.Should().Be(1, "a migration deve criar a extensão pg_trgm");
    }

    private async Task<string?> ObterIndexDefAsync(string indexName)
    {
        await using GeoDbContext context = _fixture.CreateDbContext();
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT indexdef FROM pg_indexes WHERE indexname = @nome;";
        DbParameter parametro = command.CreateParameter();
        parametro.ParameterName = "nome";
        parametro.Value = indexName;
        command.Parameters.Add(parametro);

        object? result = await command.ExecuteScalarAsync();
        return result as string;
    }

    private async Task<long> ContarIndicesUnicosSomenteCepAsync()
    {
        await using GeoDbContext context = _fixture.CreateDbContext();
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        // Índice UNIQUE cujo conjunto de colunas é exatamente "(cep)" — a vírgula em
        // "(cep, ..." de um índice composto não casa com "(cep)".
        command.CommandText =
            "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'logradouro' "
            + "AND indexdef ILIKE '%UNIQUE%' AND indexdef ILIKE '%(cep)%';";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
