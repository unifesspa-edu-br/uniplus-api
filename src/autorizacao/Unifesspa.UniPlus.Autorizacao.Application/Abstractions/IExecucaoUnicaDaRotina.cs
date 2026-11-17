namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Garante que uma rotina execute em uma só instância por vez. A execução dura até o descarte do que o início devolve e,
/// se o processo cair, termina com a conexão que a sustenta.
/// </summary>
public interface IExecucaoUnicaDaRotina
{
    /// <summary>A execução da rotina, ou nulo quando outra instância já a executa.</summary>
    Task<IAsyncDisposable?> TentarIniciarAsync(string nome, CancellationToken cancellationToken);
}
