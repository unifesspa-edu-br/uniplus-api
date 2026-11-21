namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>O que o controle faz quando a transação da retificação está prestes a ser confirmada.</summary>
internal enum ComportamentoNaConfirmacao
{
    /// <summary>Só registra o que a transação contém e deixa a confirmação seguir.</summary>
    Observar,

    /// <summary>Registra o que a transação contém e falha antes da confirmação.</summary>
    Falhar,

    /// <summary>Registra o que a transação contém e segura a confirmação até ser liberado.</summary>
    Reter,
}

/// <summary>
/// Age na confirmação da transação que gravou a versão 2 do processo armado: lê, pela mesma conexão e dentro da
/// transação, tudo o que a retificação gravou, e então deixa seguir, falha ou segura a confirmação. Só a transação que
/// enxerga a versão 2 do processo armado é alvo; as demais passam sem efeito, e o controle age uma única vez.
/// </summary>
internal sealed class ControleDaConfirmacaoDaRetificacao : DbTransactionInterceptor
{
    private static readonly TimeSpan LimiteDaEspera = TimeSpan.FromSeconds(60);

    private readonly TaskCompletionSource<ConteudoDaTransacaoDaRetificacao> _observado = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _liberada = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid _processoId;
    private ComportamentoNaConfirmacao _reacao;
    private int _armado;

    /// <summary>Arma o controle para a próxima confirmação da retificação do processo.</summary>
    public void Armar(Guid processoId, ComportamentoNaConfirmacao reacao)
    {
        _processoId = processoId;
        _reacao = reacao;
        Volatile.Write(ref _armado, 1);
    }

    /// <summary>Espera o controle agir e devolve o que a transação continha nesse instante.</summary>
    public Task<ConteudoDaTransacaoDaRetificacao> AguardarObservadoAsync() => _observado.Task.WaitAsync(LimiteDaEspera);

    /// <summary>Solta a confirmação retida; sem retenção, não faz nada.</summary>
    public void Liberar() => _liberada.TrySetResult();

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        ConteudoDaTransacaoDaRetificacao? conteudo = Volatile.Read(ref _armado) == 1
            ? await LeituraDaTransacaoDaRetificacao.LerAsync(transaction, _processoId, cancellationToken)
            : null;
        if (conteudo is not null && Interlocked.Exchange(ref _armado, 0) == 1)
        {
            _observado.TrySetResult(conteudo);
            await ReagirAsync(cancellationToken);
        }

        return result;
    }

    private async Task ReagirAsync(CancellationToken cancellationToken)
    {
        if (_reacao == ComportamentoNaConfirmacao.Falhar)
        {
            throw new InvalidOperationException("Falha antes da confirmação da retificação (simulada).");
        }

        if (_reacao == ComportamentoNaConfirmacao.Reter)
        {
            await _liberada.Task.WaitAsync(LimiteDaEspera, cancellationToken);
        }
    }
}
