namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A forma de um item lida da entrada, a mesma no processo e no modelo, conferida mesmo quando alguma parte dele não pôde
/// ser lida. O que depende de uma parte não lida não se confere; a regra em si é de <see cref="FormaDoItem"/> e de
/// <see cref="RegrasLidasDoCampo"/>.
/// </summary>
public static class FormaDoItemNaEntrada
{
    /// <summary>
    /// As recusas da forma básica, da ajuda, do formato e das regras, nessa ordem, no caminho a partir do item.
    /// </summary>
    /// <param name="tipo">O tipo de campo lido; nulo quando recusado, e então o formato e o que depende do tipo ficam sem conferir.</param>
    /// <param name="formato">O formato do fato no catálogo.</param>
    /// <param name="regras">As regras lidas; a parte nula já foi recusada e conta como conforme.</param>
    public static List<FieldError> Conferir(FatoColetadoInput entrada, TipoDeCampo? tipo, string? formato, RegrasLidasDoCampo regras)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(regras);

        return
        [
            .. FormaDoItem.ValidarFormaBasica(entrada.FatoCodigo, entrada.Ordem, entrada.Apresentacao?.Rotulo),
            .. FormaDoItem.RecusasDaAjuda(entrada.Apresentacao?.Ajuda).Select(static erro => erro.NoCaminhoDe(FatoColetadoInput.CaminhoDaApresentacao)),
            .. tipo is { } campo ? FormaDoItem.RecusasDoFormato(campo, formato) : [],
            .. regras.ConferirNoCampo(entrada.FatoCodigo ?? string.Empty, tipo)
                .Select(static erro => erro.NoCaminhoDe(FatoColetadoInput.CaminhoDasRegras)),
        ];
    }
}
