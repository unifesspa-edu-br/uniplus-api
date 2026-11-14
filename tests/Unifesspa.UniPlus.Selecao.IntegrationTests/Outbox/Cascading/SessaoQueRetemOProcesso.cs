namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;

using Npgsql;

/// <summary>
/// Uma sessão do teste que trava a linha de um processo e a mantém, em conexão própria, para que a operação real que
/// chegar depois espere por ela. O que a operação que espera já segurou é lido do catálogo do banco por outra conexão,
/// sem gancho no código de produção.
/// </summary>
internal sealed class SessaoQueRetemOProcesso : IAsyncDisposable
{
    private const string TravaDoProcesso = "SELECT 1 FROM selecao.processos_seletivos WHERE id = @processo FOR UPDATE";

    private const string SessoesEsperandoPorEsta =
        "SELECT pid::text FROM pg_stat_activity WHERE datname = current_database() AND @retida = ANY(pg_blocking_pids(pid))";

    // Leitura pega só AccessShareLock; qualquer outro modo numa tabela de Publicações ou da divulgação é escrita ou trava de linha.
    private const string TravasDeEscritaDasSessoesEmEspera = """
        SELECT n.nspname || '.' || c.relname || ' ' || l.mode
        FROM pg_locks l
        JOIN pg_class c ON c.oid = l.relation
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE l.locktype = 'relation' AND l.mode <> 'AccessShareLock'
          AND l.pid IN (SELECT pid FROM pg_stat_activity WHERE datname = current_database() AND @retida = ANY(pg_blocking_pids(pid)))
          AND (n.nspname = 'publicacoes' OR (n.nspname = 'selecao' AND c.relname LIKE 'certames\_divulgados%'))
        ORDER BY 1
        """;

    private readonly string _connectionString;
    private readonly NpgsqlConnection _conexao;
    private readonly NpgsqlTransaction _transacao;
    private readonly int _pid;

    private SessaoQueRetemOProcesso(string connectionString, NpgsqlConnection conexao, NpgsqlTransaction transacao, int pid)
    {
        _connectionString = connectionString;
        _conexao = conexao;
        _transacao = transacao;
        _pid = pid;
    }

    public static async Task<SessaoQueRetemOProcesso> AbrirAsync(string connectionString, Guid processoId)
    {
        NpgsqlConnection conexao = new(connectionString);
        await conexao.OpenAsync();
        NpgsqlTransaction transacao = await conexao.BeginTransactionAsync();
        await using (NpgsqlCommand trava = new(TravaDoProcesso, conexao, transacao))
        {
            trava.Parameters.AddWithValue("processo", processoId);
            await trava.ExecuteNonQueryAsync();
        }

        return new SessaoQueRetemOProcesso(connectionString, conexao, transacao, conexao.ProcessID);
    }

    /// <summary>Espera o banco registrar uma sessão esperando por esta trava: a operação chegou ao ponto da disputa.</summary>
    public async Task AguardarOperacaoEsperandoAsync()
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);
        while ((await ListarAsync(SessoesEsperandoPorEsta)).Count == 0)
        {
            if (DateTimeOffset.UtcNow > limite)
            {
                throw new TimeoutException($"Nenhuma sessão ficou esperando pela trava do processo mantida pela sessão {_pid.ToString(CultureInfo.InvariantCulture)}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    /// <summary>As tabelas de Publicações e da divulgação em que as sessões que esperam por esta trava seguram escrita ou trava de linha.</summary>
    public Task<IReadOnlyList<string>> TravasDeEscritaDaOperacaoEmEsperaAsync() => ListarAsync(TravasDeEscritaDasSessoesEmEspera);

    public Task LiberarAsync() => _transacao.RollbackAsync();

    public async ValueTask DisposeAsync()
    {
        await _transacao.DisposeAsync();
        await _conexao.DisposeAsync();
    }

    private async Task<IReadOnlyList<string>> ListarAsync(string consulta)
    {
        await using NpgsqlConnection observadora = new(_connectionString);
        await observadora.OpenAsync();
        await using NpgsqlCommand comando = new(consulta, observadora);
        comando.Parameters.AddWithValue("retida", _pid);
        List<string> linhas = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            linhas.Add(leitor.GetString(0));
        }

        return linhas;
    }
}
