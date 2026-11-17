namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// A remoção de um documento pendente vencido parada no passo dos objetos, com a linha já apagada e
/// ainda segura pela transação, até o teste liberá-la. Ao descartar, libera a remoção e espera ela
/// terminar antes de fechar o contexto, qualquer que seja o caminho do teste.
/// </summary>
internal sealed class RemocaoDoPendenteSegurandoALinha : IAsyncDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);

    private readonly UniPlusDbContext _context;
    private readonly TaskCompletionSource _linhaSegurada = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _liberarObjetos = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RemocaoDoPendenteSegurandoALinha(ProcessoSeletivoDbFixture banco, Guid documentoId)
    {
        ArgumentNullException.ThrowIfNull(banco);
        _context = banco.CreateDbContext();
        Resultado = new DocumentoAPublicarRepository(_context).RemoverSePendenteVencidoAsync(
            documentoId,
            TimeProvider.System.GetUtcNow(),
            async _ =>
            {
                _linhaSegurada.SetResult();
                await _liberarObjetos.Task;
            });
    }

    /// <summary>Se a linha foi removida; propaga a falha da remoção.</summary>
    public Task<bool> Resultado { get; }

    /// <summary>Espera, dentro do prazo, a remoção segurar a linha; propaga a falha dela se terminar antes.</summary>
    public async Task AguardarLinhaSeguradaAsync()
    {
        await await Task.WhenAny(_linhaSegurada.Task, Resultado, Task.Delay(Prazo));
        _linhaSegurada.Task.IsCompleted.Should().BeTrue($"a remoção deve segurar a linha dentro de {Prazo}");
    }

    public void Liberar() => _liberarObjetos.TrySetResult();

    public async ValueTask DisposeAsync()
    {
        Liberar();
        await Task.WhenAny(Resultado);
        await _context.DisposeAsync();
    }
}
