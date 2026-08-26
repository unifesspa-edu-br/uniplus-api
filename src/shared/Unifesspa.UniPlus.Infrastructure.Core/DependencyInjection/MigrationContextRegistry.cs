namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// Os contextos cujas migrations o processo deve aplicar, coletados no registro de cada módulo.
/// </summary>
/// <remarks>
/// Existe para que <c>ApplyAndExit</c> não precise passar por
/// <c>GetServices&lt;IHostedService&gt;()</c>: aquela chamada constrói a coleção
/// <b>inteira</b> antes de qualquer filtro, incluindo o warmup de criptografia, o runtime de
/// mensageria e o cliente do schema registry. Um Job de migration falharia ao construir
/// qualquer um deles — por configuração que não tem relação alguma com schema — e o rollout
/// seria abortado por um motivo falso.
/// <para>Cada entrada guarda a operação já fechada sobre o seu tipo de contexto, o que evita
/// reflexão na hora de aplicar.</para>
/// </remarks>
internal sealed class MigrationContextRegistry
{
    private readonly List<(Type Contexto, Func<IServiceProvider, CancellationToken, Task> Aplicar)> _entradas = [];

    /// <summary>Contextos registrados, na ordem em que os módulos os declararam.</summary>
    public IReadOnlyList<(Type Contexto, Func<IServiceProvider, CancellationToken, Task> Aplicar)> Entradas =>
        _entradas;

    /// <summary>
    /// Registra um contexto. Repetir o mesmo tipo é ignorado — o registro é defensivo contra
    /// dupla-chamada, do mesmo modo que o hosted service correspondente.
    /// </summary>
    public void Registrar(Type contexto, Func<IServiceProvider, CancellationToken, Task> aplicar)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(aplicar);

        if (_entradas.Any(e => e.Contexto == contexto))
        {
            return;
        }

        _entradas.Add((contexto, aplicar));
    }
}
