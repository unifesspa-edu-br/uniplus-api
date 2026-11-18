namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

/// <summary>
/// O início simultâneo de operações concorrentes: cada participante avisa que está pronto e espera, e só quando o último chega todos
/// seguem, de modo que nenhum envio começa antes de os outros estarem prontos para começar.
/// </summary>
internal sealed class InicioSimultaneoDeTeste(int participantes)
{
    private readonly TaskCompletionSource _liberada = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _prontos;

    public Task AguardarOsOutrosAsync()
    {
        if (Interlocked.Increment(ref _prontos) == participantes)
        {
            _liberada.SetResult();
        }

        return _liberada.Task;
    }
}
