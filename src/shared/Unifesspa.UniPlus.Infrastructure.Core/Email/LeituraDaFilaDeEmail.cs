namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Npgsql;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Wolverine;
using Wolverine.Postgresql.Transport;
using Wolverine.Runtime;
using Wolverine.Util;

/// <summary>As tabelas do banco do outbox que o retrato lê, já citadas: a fila de envio e as mensagens mortas.</summary>
public sealed record TabelasDoEnvioDeEmail(string Fila, string Mortas)
{
    /// <summary>
    /// A fila vive no esquema do transporte e as mortas no esquema da persistência; os nomes vêm da configuração do
    /// Wolverine em execução, não de texto escrito à mão.
    /// </summary>
    public static TabelasDoEnvioDeEmail Do(IWolverineRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        PostgresqlTransport transporte = runtime.Options.Transports.GetOrCreate<PostgresqlTransport>();

        return new TabelasDoEnvioDeEmail(
            $"{Citar(transporte.TransportSchemaName)}.{Citar("wolverine_queue_" + transporte.SanitizeIdentifier(EnvioDeEmailServiceCollectionExtensions.FilaDeEnvio))}",
            $"{Citar(WolverineOutboxConfiguration.PersistenceSchema)}.{Citar("wolverine_dead_letters")}");
    }

    private static string Citar(string identificador)
    {
        using NpgsqlCommandBuilder construtor = new();

        return construtor.QuoteIdentifier(identificador);
    }
}

/// <summary>
/// Lê, direto do banco do outbox, o retrato da fila durável de e-mail: as linhas prontas, a idade da mais antiga, as que
/// ninguém retirou e o estoque de mensagens de e-mail na fila de mensagens mortas.
/// </summary>
public sealed class LeituraDaFilaDeEmail
{
    /// <summary>A idade a partir da qual uma pronta conta como atrasada.</summary>
    public static readonly TimeSpan LimiteDeAtraso = TimeSpan.FromMinutes(5);

    private readonly string _stringDeConexao;
    private readonly TabelasDoEnvioDeEmail _tabelas;

    public LeituraDaFilaDeEmail(string stringDeConexao, TabelasDoEnvioDeEmail tabelas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stringDeConexao);
        ArgumentNullException.ThrowIfNull(tabelas);

        _stringDeConexao = stringDeConexao;
        _tabelas = tabelas;
    }

    public async Task<AmostraDaFilaDeEmail> LerAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection conexao = new(_stringDeConexao);
        await using (conexao.ConfigureAwait(false))
        {
            await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
            (long prontas, TimeSpan idade, long atrasadas) = await LerAFilaAsync(conexao, cancellationToken).ConfigureAwait(false);

            return new AmostraDaFilaDeEmail(prontas, idade, atrasadas, await ContarAsMortasAsync(conexao, cancellationToken).ConfigureAwait(false));
        }
    }

    private async Task<(long Prontas, TimeSpan Idade, long Atrasadas)> LerAFilaAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
    {
        NpgsqlCommand comando = new(ConsultaDaFila(_tabelas.Fila), conexao);
        await using (comando.ConfigureAwait(false))
        {
            comando.Parameters.AddWithValue("limite", LimiteDeAtraso);
            NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (leitor.ConfigureAwait(false))
            {
                await leitor.ReadAsync(cancellationToken).ConfigureAwait(false);

                return (leitor.GetInt64(0), TimeSpan.FromSeconds(Math.Max(0, leitor.GetDouble(1))), leitor.GetInt64(2));
            }
        }
    }

    private static string ConsultaDaFila(string tabela) =>
        $"""
        SELECT count(*),
               COALESCE(EXTRACT(EPOCH FROM (now()::timestamp - min("timestamp"))), 0)::double precision,
               count(*) FILTER (WHERE "timestamp" < now()::timestamp - @limite)
        FROM {tabela}
        """;

    private async Task<long> ContarAsMortasAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
    {
        NpgsqlCommand comando = new($"SELECT count(*) FROM {_tabelas.Mortas} WHERE message_type = @tipo", conexao);
        await using (comando.ConfigureAwait(false))
        {
            comando.Parameters.AddWithValue("tipo", typeof(EnviarEmail).ToMessageTypeName());

            return (long)(await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }
}
