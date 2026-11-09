namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Garante que uma rotina rode em uma só instância por vez. A trava dura até o descarte do que a obtenção devolve e,
/// se o processo cair, termina com a conexão que a sustenta.
/// </summary>
public interface ITravaDaRotina
{
    /// <summary>A trava, ou nulo quando outra instância já a tem.</summary>
    Task<IAsyncDisposable?> TentarObterAsync(string nome, CancellationToken cancellationToken);
}
