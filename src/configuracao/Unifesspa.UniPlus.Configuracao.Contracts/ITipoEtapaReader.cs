namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Lê exclusivamente tipos de etapa ativos (UNI-REQ-0015, UNI-REQ-0087).</summary>
public interface ITipoEtapaReader
{
    Task<IReadOnlyList<TipoEtapaView>> ListarAtivosAsync(CancellationToken cancellationToken = default);
    Task<TipoEtapaView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Busca pelo código exato, sem aparar nem normalizar.</summary>
    Task<TipoEtapaView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os tipos de etapa ativos dos <paramref name="codigos"/>, numa só leitura, ordenados por <c>Codigo</c>
    /// ascendente. O código sem tipo ativo — inexistente ou desativado — não aparece no resultado, e um código
    /// repetido no lote devolve o tipo uma só vez. A busca é pelo código exato, sem aparar nem normalizar.
    /// </summary>
    Task<IReadOnlyList<TipoEtapaView>> ListarAtivosPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken cancellationToken = default);
}
