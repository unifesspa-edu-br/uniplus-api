namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using AwesomeAssertions;

using Npgsql;

/// <summary>
/// A linha semeada NOTA_ENEM é compartilhada pela coleção. Guarda o estado dela antes do
/// teste e o repõe no fim, para uma regressão que a altere não contaminar os demais testes.
/// </summary>
internal sealed class TipoDoEnemRestauradoNoFim : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly object?[] _estado;

    private TipoDoEnemRestauradoNoFim(string connectionString, object?[] estado)
    {
        _connectionString = connectionString;
        _estado = estado;
    }

    public static async Task<TipoDoEnemRestauradoNoFim> GuardarAsync(string connectionString)
    {
        await using NpgsqlConnection conexao = new(connectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            "SELECT nome, descricao, ativo, admite_pontuacao, admite_eliminacao, nota_de_origem_no_enem " +
            "FROM configuracao.tipos_etapa WHERE codigo = 'NOTA_ENEM'", conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue("pré-condição: a carga do cadastro semeia NOTA_ENEM");
        object?[] estado = new object?[leitor.FieldCount];
        leitor.GetValues(estado!);
        return new(connectionString, estado);
    }

    public async ValueTask DisposeAsync()
    {
        await using NpgsqlConnection conexao = new(_connectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            "UPDATE configuracao.tipos_etapa SET nome = $1, descricao = $2, ativo = $3, admite_pontuacao = $4, " +
            "admite_eliminacao = $5, nota_de_origem_no_enem = $6 WHERE codigo = 'NOTA_ENEM'", conexao);
        foreach (object? valor in _estado)
        {
            comando.Parameters.Add(new NpgsqlParameter { Value = valor ?? DBNull.Value });
        }

        await comando.ExecuteNonQueryAsync();
    }
}
