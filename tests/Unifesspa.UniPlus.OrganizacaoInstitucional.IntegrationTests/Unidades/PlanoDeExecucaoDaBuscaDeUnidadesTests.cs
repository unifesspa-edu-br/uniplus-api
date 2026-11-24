namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Data.Common;
using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>
/// Os índices trigrama das colunas <c>*_para_busca</c> podem ser usados pela consulta que a listagem emite.
/// </summary>
/// <remarks>
/// A consulta é a que o EF emite para o recorte da busca, com os parâmetros ligados: literal embutido daria outro
/// plano. Com varreduras sequenciais desativadas e custo elevado por entrada de índice, o plano mostra que os índices
/// <b>podem</b> ser usados; não prova que o planejador os escolhe no uso normal. O volume da amostra e o
/// <c>ANALYZE</c> existem para que as estatísticas reflitam uma tabela com milhares de linhas, e o termo é raro nela.
/// </remarks>
public sealed class PlanoDeExecucaoDaBuscaDeUnidadesTests : IClassFixture<UnidadeDbFixture>, IAsyncLifetime
{
    private const int Volume = 5000;
    private const string TermoRaro = "raridade";

    private static readonly string[] CamposPesquisaveis = ["nome", "sigla", "codigo", "identificador_no_endereco_web", "apelido"];

    private readonly UnidadeDbFixture _fixture;

    public PlanoDeExecucaoDaBuscaDeUnidadesTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        await contexto.Database.ExecuteSqlRawAsync("TRUNCATE TABLE organizacao.unidades CASCADE");

        contexto.Unidades.AddRange(Enumerable.Range(0, Volume).Select(UnidadeDaAmostra));
        await contexto.SaveChangesAsync();

        // O planejador decide pelas estatísticas, que sem isto refletem a tabela vazia.
        await contexto.Database.ExecuteSqlRawAsync("ANALYZE organizacao.unidades");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A busca por texto pode usar os cinco índices trigrama, sem varrer a tabela")]
    public async Task Busca_PodeUsarOsCincoIndicesTrigrama()
    {
        CapturadorDeConsulta consulta = await CapturarConsultaDaBuscaAsync();

        string plano = await ExplicarSemVarreduraAsync(consulta);

        plano.Should().Contain("BitmapOr");
        foreach (string campo in CamposPesquisaveis)
        {
            plano.Should().Contain($"Bitmap Index Scan on ix_unidades_{campo}_para_busca_trgm");
        }

        plano.Should().NotContain("Seq Scan on unidades");
    }

    /// <summary>Uma em cada mil unidades traz o termo raro no nome; as demais não têm nada parecido.</summary>
    private static Unidade UnidadeDaAmostra(int indice)
    {
        string numero = indice.ToString("D5", CultureInfo.InvariantCulture);
        string nome = indice % 1000 == 0 ? $"Setor {TermoRaro} {numero}" : $"Setor comum {numero}";
        return new CadastroDeUnidadeDeTeste(nome, $"u-{numero}", $"S{numero}", $"C{numero}", Alias: null).Criar();
    }

    private async Task<CapturadorDeConsulta> CapturarConsultaDaBuscaAsync()
    {
        CapturadorDeConsulta capturador = new();
        await using MonolitoDbContext contexto = new(OpcoesCapturandoCom(capturador));
        FiltroListagemUnidades filtro = new(TermoRaro, []);
        List<Unidade> encontradas = await contexto.Unidades.AsNoTracking().Filtradas(filtro).ToListAsync();

        encontradas.Should().HaveCount(Volume / 1000, "o termo raro está em uma unidade a cada mil");
        return capturador;
    }

    private DbContextOptions<MonolitoDbContext> OpcoesCapturandoCom(CapturadorDeConsulta capturador) =>
        new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext: null),
                new AuditableInterceptor(TimeProvider.System, userContext: null),
                capturador)
            .Options;

    private async Task<string> ExplicarSemVarreduraAsync(CapturadorDeConsulta consulta)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync(CancellationToken.None);

        await using (NpgsqlCommand desligar = new("SET enable_seqscan = off; SET cpu_index_tuple_cost = 1", conexao))
        {
            await desligar.ExecuteNonQueryAsync(CancellationToken.None);
        }

        await using NpgsqlCommand explicar = consulta.ParaExplicar(conexao);
        return await LerPlanoAsync(explicar);
    }

    private static async Task<string> LerPlanoAsync(NpgsqlCommand explicar)
    {
        List<string> linhas = [];
        await using NpgsqlDataReader leitor = await explicar.ExecuteReaderAsync(CancellationToken.None);
        while (await leitor.ReadAsync(CancellationToken.None))
        {
            linhas.Add(leitor.GetString(0));
        }

        return string.Join('\n', linhas);
    }

    /// <summary>Guarda o SQL e os parâmetros da consulta emitida pelo EF.</summary>
    private sealed class CapturadorDeConsulta : DbCommandInterceptor
    {
        public string Sql { get; private set; } = string.Empty;

        public IReadOnlyList<(string Nome, object? Valor)> Parametros { get; private set; } = [];

        /// <summary>O <c>EXPLAIN</c> da consulta capturada, com os mesmos parâmetros ligados.</summary>
        public NpgsqlCommand ParaExplicar(NpgsqlConnection conexao)
        {
            NpgsqlCommand explicar = new("EXPLAIN " + Sql, conexao);
            foreach ((string nome, object? valor) in Parametros)
            {
                explicar.Parameters.AddWithValue(nome, valor ?? DBNull.Value);
            }

            return explicar;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            Sql = command.CommandText;
            Parametros = [.. command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, (object?)p.Value))];
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
