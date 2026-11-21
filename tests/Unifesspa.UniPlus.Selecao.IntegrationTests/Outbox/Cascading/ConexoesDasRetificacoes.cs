namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Collections.Concurrent;
using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

/// <summary>
/// Guarda o processo do banco de cada transação aberta por uma retificação marcada: é como o teste sabe qual sessão do banco
/// pertence a qual retificação, em vez de aceitar qualquer sessão que esteja esperando.
/// </summary>
internal sealed class ConexoesDasRetificacoes : DbTransactionInterceptor
{
    private readonly AsyncLocal<Guid?> _marca = new();

    private readonly ConcurrentDictionary<Guid, ConcurrentBag<int>> _processosDoBanco = new();

    /// <summary>Marca a retificação em andamento neste fluxo de execução: as transações que ela abrir passam a ser dela.</summary>
    public void Marcar(Guid documentoDaRetificacao) => _marca.Value = documentoDaRetificacao;

    /// <summary>Os processos do banco que atenderam as transações da retificação do documento, até agora.</summary>
    public IReadOnlyCollection<int> ProcessosDoBancoDe(Guid documentoDaRetificacao) =>
        _processosDoBanco.TryGetValue(documentoDaRetificacao, out ConcurrentBag<int>? processos) ? [.. processos] : [];

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        if (_marca.Value is Guid documento && connection is NpgsqlConnection npgsql)
        {
            _processosDoBanco.GetOrAdd(documento, static _ => []).Add(npgsql.ProcessID);
        }

        return ValueTask.FromResult(result);
    }
}
