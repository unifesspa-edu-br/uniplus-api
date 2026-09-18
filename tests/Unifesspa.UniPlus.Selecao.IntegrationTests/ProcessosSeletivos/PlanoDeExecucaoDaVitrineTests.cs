namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Xunit;

/// <summary>
/// Os índices da vitrine são escolhidos pelo planejador para as consultas que a listagem emite.
/// </summary>
/// <remarks>
/// Índice cuja expressão não casa a consulta é mantido a cada escrita e nunca usado — sem sintoma
/// além da lentidão sob volume. O volume da amostra e o <c>ANALYZE</c> existem porque abaixo deles
/// o planejador varre por ser mais barato, e a asserção mediria o tamanho da tabela.
/// </remarks>
public sealed class PlanoDeExecucaoDaVitrineTests : IClassFixture<ProcessoSeletivoDbFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Volume a partir do qual o planejador prefere índice à varredura.</summary>
    private const int Volume = 4000;

    private readonly ProcessoSeletivoDbFixture _fixture;

    public PlanoDeExecucaoDaVitrineTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE selecao.certames_divulgados");

        List<CertameDivulgado> linhas = [];
        for (int i = 0; i < Volume; i++)
        {
            linhas.Add(CertameDivulgado.Criar(
                Guid.CreateVersion7(),
                numeroVersao: 1,
                Guid.CreateVersion7(),
                new string('a', 64),
                versaoProjecao: "1",
                new FacetasDoCertameDivulgado(
                    string.Create(CultureInfo.InvariantCulture, $"Certame {i:D5}"),
                    string.Create(CultureInfo.InvariantCulture, $"{i:D5}/2026"),
                    ModalidadesDe(i),
                    Agora.AddDays(-60),
                    Agora.AddDays((i % 400) - 200)),
                """{"nome":"documento"}""",
                Agora));
        }

        context.CertamesDivulgados.AddRange(linhas);
        await context.SaveChangesAsync();

        // O planejador decide pelas estatísticas, que sem isto refletem a tabela vazia.
        await context.Database.ExecuteSqlRawAsync("ANALYZE selecao.certames_divulgados");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Modalidades da amostra, com distribuição desigual: ampla concorrência em todos, reserva de
    /// pessoa com deficiência em pouco mais de 1%.
    /// </summary>
    /// <remarks>
    /// Recorte que casa metade da tabela é servido melhor por varredura, e o planejador acerta ao
    /// preferi-la. A desigualdade imita a seletividade real da modalidade.
    /// </remarks>
    private static IReadOnlyList<string> ModalidadesDe(int indice) =>
        indice % 80 == 0 ? ["AC", "PCD"] : ["AC"];

    [Fact(DisplayName = "O recorte por prazo com ordem e desempate usa o índice, sem varrer a tabela")]
    public async Task Recorte_QuandoFaixaDePrazoComOrdem_DeveUsarOIndiceDoPrazo()
    {
        // A forma de toda listagem da vitrine, e a que ix_certames_divulgados_prazo declara.
        string plano = await ExplicarAsync(
            """
            SELECT id FROM selecao.certames_divulgados
            WHERE inscricoes_ate >= $1 AND inscricoes_ate < $2
            ORDER BY inscricoes_ate, id
            LIMIT 24
            """,
            Agora,
            Agora.AddDays(7));

        plano.Should().Contain("ix_certames_divulgados_prazo", "é o índice que existe para esta forma de consulta");
        plano.Should().NotContain("Seq Scan on certames_divulgados", "varrer a tabela é o que o índice existe para evitar");
    }

    [Fact(DisplayName = "A ordem alfabética por título usa o índice da chave de ordenação")]
    public async Task Ordenacao_QuandoAlfabeticaPorTitulo_DeveUsarOIndiceDaChave()
    {
        string plano = await ExplicarAsync(
            """
            SELECT id FROM selecao.certames_divulgados
            ORDER BY nome_ordenacao, id
            LIMIT 24
            """);

        plano.Should().Contain("ix_certames_divulgados_nome_ordenacao");
        plano.Should().NotContain("Seq Scan on certames_divulgados");
    }

    [Fact(DisplayName = "O recorte por modalidade usa o índice GIN do arranjo")]
    public async Task Modalidade_QuandoRecortaPorPertinencia_DeveUsarOIndiceGin()
    {
        // A modalidade consultada é a rara da amostra — pela comum, varrer seria o plano correto.
        string plano = await ExplicarAsync(
            """
            SELECT id FROM selecao.certames_divulgados
            WHERE modalidades_ofertadas @> ARRAY[$1]::text[]
            """,
            "PCD");

        plano.Should().Contain("ix_certames_divulgados_modalidades");
        plano.Should().NotContain("Seq Scan on certames_divulgados");
    }

    /// <summary>
    /// Plano escolhido pelo Postgres, com os parâmetros ligados — literais embutidos produzem outro
    /// plano.
    /// </summary>
    private async Task<string> ExplicarAsync(string sql, params object[] parametros)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync(CancellationToken.None);

        await using NpgsqlCommand comando = new("EXPLAIN " + sql, conexao);
        foreach (object parametro in parametros)
        {
            comando.Parameters.AddWithValue(parametro);
        }

        List<string> linhas = [];
        await using (NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(CancellationToken.None))
        {
            while (await leitor.ReadAsync(CancellationToken.None))
            {
                linhas.Add(leitor.GetString(0));
            }
        }

        return string.Join('\n', linhas);
    }
}
