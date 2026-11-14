namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Grava os avisos emitidos na conexão e na transação do <see cref="UniPlusDbContext"/> de quem chama, por
/// <c>INSERT ... ON CONFLICT DO NOTHING RETURNING</c>. Uma segunda gravação concorrente do mesmo identificador espera a
/// primeira confirmar e não recebe nada de volta, de modo que só uma emite a mensagem. Sem transação aberta o registro
/// não seria atômico com o outbox, e a gravação recusa.
/// </summary>
public sealed class RegistroDeAvisos : IRegistroDeAvisos
{
    private const string Comando = $"""
        INSERT INTO {AutorizacaoContribuicao.NomeDoEsquema}.avisos_emitidos (mensagem_id, tipo, fato_id, emitido_em)
        SELECT id, @tipo, @fato, @emitido FROM unnest(@ids) WITH ORDINALITY AS u(id, ordem) ORDER BY ordem
        ON CONFLICT (mensagem_id) DO NOTHING
        RETURNING mensagem_id
        """;

    private readonly UniPlusDbContext _context;
    private readonly TimeProvider _clock;

    public RegistroDeAvisos(UniPlusDbContext context, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);

        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<Guid>> RegistrarAsync(
        string tipo, Guid fatoId, IReadOnlyList<Guid> mensagensIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        ArgumentNullException.ThrowIfNull(mensagensIds);

        if (mensagensIds.Count == 0)
        {
            return [];
        }

        IDbContextTransaction transacao = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("O registro de avisos exige a transação da operação que emite o aviso.");

        DbCommand comando = ComandoDoRegistro(transacao, tipo, fatoId, mensagensIds);
        await using (comando.ConfigureAwait(false))
        {
            return await LerAsync(comando, cancellationToken).ConfigureAwait(false);
        }
    }

    private DbCommand ComandoDoRegistro(IDbContextTransaction transacao, string tipo, Guid fatoId, IReadOnlyList<Guid> mensagensIds)
    {
        DbCommand comando = _context.Database.GetDbConnection().CreateCommand();
        comando.Transaction = transacao.GetDbTransaction();
        comando.CommandText = Comando;
        Parametro(comando, "tipo", tipo);
        Parametro(comando, "fato", fatoId);
        Parametro(comando, "emitido", _clock.GetUtcNow());
        Parametro(comando, "ids", mensagensIds.ToArray());

        return comando;
    }

    private static void Parametro(DbCommand comando, string nome, object valor)
    {
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }

    private static async Task<IReadOnlyCollection<Guid>> LerAsync(DbCommand comando, CancellationToken cancellationToken)
    {
        List<Guid> novos = [];
        DbDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (leitor.ConfigureAwait(false))
        {
            while (await leitor.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                novos.Add(leitor.GetGuid(0));
            }
        }

        return novos;
    }
}
