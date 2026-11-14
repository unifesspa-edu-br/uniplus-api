namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Logging;

using Npgsql;

/// <summary>
/// Aplica scripts SQL em ordem, cada um numa transação própria: o script que falha desfaz só a si mesmo e os anteriores
/// permanecem confirmados. Repetir a aplicação é responsabilidade do script, que deve ser idempotente.
/// </summary>
internal static partial class ExecutorDeScriptsSql
{
    public static async Task ExecutarAsync(
        string cadeiaDeConexao,
        IReadOnlyList<ScriptSql> scripts,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (scripts.Count == 0)
        {
            return;
        }

        NpgsqlConnection conexao = new(cadeiaDeConexao);
        await using (conexao.ConfigureAwait(false))
        {
            await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
            foreach (ScriptSql script in scripts)
            {
                await AplicarAsync(conexao, script, logger, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task AplicarAsync(
        NpgsqlConnection conexao,
        ScriptSql script,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecutarEmTransacaoAsync(conexao, script.Texto, cancellationToken).ConfigureAwait(false);
        }
        catch (NpgsqlException excecao)
        {
            throw new InvalidOperationException($"Falha ao aplicar o script SQL '{script.Nome}'.", excecao);
        }

        LogScriptAplicado(logger, script.Nome);
    }

    private static async Task ExecutarEmTransacaoAsync(
        NpgsqlConnection conexao,
        string texto,
        CancellationToken cancellationToken)
    {
        NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transacao.ConfigureAwait(false))
        {
            NpgsqlCommand comando = new(texto, conexao, transacao);
            await using (comando.ConfigureAwait(false))
            {
                await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 3021, Level = LogLevel.Information, Message = "Script SQL aplicado: {Script}.")]
    private static partial void LogScriptAplicado(ILogger logger, string script);
}
