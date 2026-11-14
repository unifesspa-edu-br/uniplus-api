namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Logging;

using Npgsql;

/// <summary>
/// A trava de sessão que serializa, por banco, a subida dos módulos: quem a detém é o único processo aplicando scripts,
/// migrations e a etapa posterior. Vive numa conexão só dela, e é liberada ao descartar.
/// </summary>
internal sealed partial class TravaDeMigrationDoBanco : IAsyncDisposable
{
    // Diferente da chave que o provider Npgsql usa dentro do MigrateAsync, em outra conexão: chaves iguais fariam esta
    // subida esperar por si mesma.
    private const long Chave = 0x554E49504C555300L;

    private readonly NpgsqlConnection _conexao;
    private readonly ILogger _logger;

    private TravaDeMigrationDoBanco(NpgsqlConnection conexao, ILogger logger)
    {
        _conexao = conexao;
        _logger = logger;
    }

    public static async Task<TravaDeMigrationDoBanco> AdquirirAsync(
        string cadeiaDeConexao,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection conexao = new(cadeiaDeConexao);
        try
        {
            await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecutarAsync(conexao, "SELECT pg_advisory_lock($1)", cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await conexao.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new TravaDeMigrationDoBanco(conexao, logger);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await ExecutarAsync(_conexao, "SELECT pg_advisory_unlock($1)", CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (NpgsqlException excecao)
        {
            // A conexão perdida já liberou a trava no servidor; a falha original, se houver, não deve ser mascarada.
            LogFalhaAoLiberarATrava(_logger, excecao);
        }

        await _conexao.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string comando, CancellationToken cancellationToken)
    {
        NpgsqlCommand cmd = new(comando, conexao);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = Chave });
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        EventId = 3020,
        Level = LogLevel.Warning,
        Message = "Falha ao liberar a trava de migration do banco; a conexão será descartada.")]
    private static partial void LogFalhaAoLiberarATrava(ILogger logger, Exception excecao);
}
