namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// O tipo da Unidade que o pedido informou: a única leitura dele, na criação e na atualização. O código ausente
/// ou desconhecido não tem tipo; a recusa e a lista dos códigos aceitos são de <c>Unidade.ValidarCampos</c>.
/// </summary>
internal static class TipoDaUnidadeDaEntrada
{
    internal static TipoUnidade? Ler(string? codigo) =>
        CatalogoDe<TipoUnidade>.TentarObter(codigo, out TipoUnidade tipo) ? tipo : null;
}
