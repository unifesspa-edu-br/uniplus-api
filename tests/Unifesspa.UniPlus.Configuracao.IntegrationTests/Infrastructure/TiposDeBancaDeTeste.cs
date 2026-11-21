namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using Npgsql;

/// <summary>
/// O banco dos testes nasce com os nove tipos de banca da semente, e o vocabulário é fechado: quem cadastra um tipo pelo
/// próprio teste precisa antes liberar o código que a semente ocupa.
/// </summary>
internal static class TiposDeBancaDeTeste
{
    /// <summary>Apaga de vez os tipos de banca dos códigos informados, vivos ou removidos; sem códigos, apaga todos.</summary>
    public static async Task ApagarAsync(string cadeiaDeConexao, params string[] codigos)
    {
        await using NpgsqlConnection conexao = new(cadeiaDeConexao);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            "DELETE FROM configuracao.tipos_de_banca WHERE cardinality(@codigos) = 0 OR codigo = ANY(@codigos)", conexao);
        comando.Parameters.Add(new NpgsqlParameter("codigos", codigos));
        await comando.ExecuteNonQueryAsync();
    }
}
