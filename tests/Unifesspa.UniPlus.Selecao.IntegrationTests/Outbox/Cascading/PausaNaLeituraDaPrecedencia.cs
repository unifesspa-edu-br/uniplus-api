namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Segura, uma única vez, a primeira consulta ao cadastro de precedência entre fases até o teste liberar, e avisa quando a consulta
/// chegou: o teste usa o intervalo para olhar o que o banco mostra enquanto a operação está parada nessa leitura.
/// </summary>
internal sealed class PausaNaLeituraDaPrecedencia : DbCommandInterceptor
{
    private const string TabelaDoCadastro = "precedencias_entre_fases";

    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(60);

    private readonly TaskCompletionSource _chegou = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _liberada = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pausou;

    /// <summary>Espera a operação chegar à consulta do cadastro de precedência.</summary>
    public Task AguardarALeituraAsync() => _chegou.Task.WaitAsync(Limite);

    /// <summary>Deixa a consulta retida seguir; sem retenção, não faz nada.</summary>
    public void Liberar() => _liberada.TrySetResult();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CommandText.Contains(TabelaDoCadastro, StringComparison.Ordinal) && Interlocked.Exchange(ref _pausou, 1) == 0)
        {
            _chegou.TrySetResult();
            await _liberada.Task.WaitAsync(Limite, cancellationToken);
        }

        return result;
    }
}
