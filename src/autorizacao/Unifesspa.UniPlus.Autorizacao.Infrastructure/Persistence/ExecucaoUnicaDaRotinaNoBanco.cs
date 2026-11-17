namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A execução única da rotina como bloqueio consultivo de sessão do PostgreSQL, numa conexão dedicada e fora da transação
/// de qualquer operação: o fim da conexão a libera.
/// </summary>
internal sealed class ExecucaoUnicaDaRotinaNoBanco : IExecucaoUnicaDaRotina
{
    private readonly UniPlusDbContext _db;

    public ExecucaoUnicaDaRotinaNoBanco(UniPlusDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        _db = db;
    }

    public async Task<IAsyncDisposable?> TentarIniciarAsync(string nome, CancellationToken cancellationToken)
    {
        NpgsqlConnection conexao = new(_db.Database.GetConnectionString());

        return await TentarNaConexaoAsync(conexao, nome, cancellationToken).ConfigureAwait(false)
            ? new ExecucaoUnicaObtida(conexao, nome)
            : null;
    }

    /// <summary>Pede a execução única na conexão dedicada; a conexão que não ficou com ela é descartada aqui, também quando abrir ou pedir falha.</summary>
    private static async Task<bool> TentarNaConexaoAsync(NpgsqlConnection conexao, string nome, CancellationToken cancellationToken)
    {
        bool obtida = false;
        try
        {
            obtida = await PedirExecucaoUnicaAsync(conexao, nome, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!obtida)
            {
                await conexao.DisposeAsync().ConfigureAwait(false);
            }
        }

        return obtida;
    }

    private static async Task<bool> PedirExecucaoUnicaAsync(NpgsqlConnection conexao, string nome, CancellationToken cancellationToken)
    {
        await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
        NpgsqlCommand comando = new("select pg_try_advisory_lock(hashtext(@nome))", conexao);
        await using (comando.ConfigureAwait(false))
        {
            comando.Parameters.AddWithValue("nome", nome);

            return await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
        }
    }

    private sealed class ExecucaoUnicaObtida : IAsyncDisposable
    {
        private readonly NpgsqlConnection _conexao;
        private readonly string _nome;

        public ExecucaoUnicaObtida(NpgsqlConnection conexao, string nome)
        {
            _conexao = conexao;
            _nome = nome;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                NpgsqlCommand comando = new("select pg_advisory_unlock(hashtext(@nome))", _conexao);
                await using (comando.ConfigureAwait(false))
                {
                    comando.Parameters.AddWithValue("nome", _nome);
                    await comando.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                await _conexao.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
