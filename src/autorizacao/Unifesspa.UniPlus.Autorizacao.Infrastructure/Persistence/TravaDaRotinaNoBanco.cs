namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// A trava de instância única como trava de sessão do PostgreSQL, numa conexão dedicada e fora da transação de qualquer
/// operação: o fim da conexão a libera.
/// </summary>
internal sealed class TravaDaRotinaNoBanco : ITravaDaRotina
{
    private readonly AutorizacaoDbContext _db;

    public TravaDaRotinaNoBanco(AutorizacaoDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        _db = db;
    }

    public async Task<IAsyncDisposable?> TentarObterAsync(string nome, CancellationToken cancellationToken)
    {
        NpgsqlConnection conexao = new(_db.Database.GetConnectionString());
        bool obtida = false;
        try
        {
            await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
            obtida = await TentarAsync(conexao, nome, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!obtida)
            {
                await conexao.DisposeAsync().ConfigureAwait(false);
            }
        }

        return obtida ? new TravaObtida(conexao, nome) : null;
    }

    private static async Task<bool> TentarAsync(NpgsqlConnection conexao, string nome, CancellationToken cancellationToken)
    {
        NpgsqlCommand comando = new("select pg_try_advisory_lock(hashtext(@nome))", conexao);
        await using (comando.ConfigureAwait(false))
        {
            comando.Parameters.AddWithValue("nome", nome);

            return await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
        }
    }

    private sealed class TravaObtida : IAsyncDisposable
    {
        private readonly NpgsqlConnection _conexao;
        private readonly string _nome;

        public TravaObtida(NpgsqlConnection conexao, string nome)
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
