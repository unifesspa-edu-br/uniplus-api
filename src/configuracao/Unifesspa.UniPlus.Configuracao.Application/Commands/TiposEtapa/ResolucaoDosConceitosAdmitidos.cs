namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve os códigos de conceito do comando contra o catálogo. A lista ausente ou vazia não consulta o
/// catálogo: não há o que resolver.
/// </summary>
internal static class ResolucaoDosConceitosAdmitidos
{
    public static async Task<Result<IReadOnlyList<TipoConceito>>> Resolver(
        IReadOnlyList<string>? codigos, ITipoConceitoRepository conceitos, CancellationToken cancellationToken)
    {
        ConceitosPedidos pedidos = ConceitosPedidos.De(codigos);
        IReadOnlyList<TipoConceito> catalogo = pedidos.CodigosParaConsulta.Count == 0
            ? []
            : await conceitos.ListarPorCodigosAsync(pedidos.CodigosParaConsulta, cancellationToken).ConfigureAwait(false);
        return pedidos.Resolver(catalogo);
    }
}
