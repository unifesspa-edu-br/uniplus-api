namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Npgsql;

using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>Confere, no catálogo do banco, que alguma sessão está parada esperando o bloqueio de uma linha.</summary>
internal static class SessaoEsperandoBloqueio
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(10);

    /// <summary>Espera até que uma sessão esteja esperando o bloqueio da linha pedida por <paramref name="trechoDoComando"/>.</summary>
    public static async Task AguardarAsync(ProcessoSeletivoDbFixture fixture, string trechoDoComando)
    {
        await using NpgsqlConnection conexao = new(fixture.ConnectionString);
        await conexao.OpenAsync();
        DateTimeOffset prazo = DateTimeOffset.UtcNow + Limite;

        while (!await HaSessaoEsperandoAsync(conexao, trechoDoComando))
        {
            if (DateTimeOffset.UtcNow > prazo)
            {
                throw new TimeoutException($"Nenhuma sessão esperou o bloqueio de '{trechoDoComando}' dentro de {Limite}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    private static async Task<bool> HaSessaoEsperandoAsync(NpgsqlConnection conexao, string trechoDoComando)
    {
        await using NpgsqlCommand comando = new(
            "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%' || @trecho || '%'", conexao);
        comando.Parameters.AddWithValue("trecho", trechoDoComando);
        return (long)(await comando.ExecuteScalarAsync())! > 0;
    }
}
