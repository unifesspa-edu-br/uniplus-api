namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// O tipo de campo que o pedido informou para um item do formulário: a única leitura dele, no processo e no modelo.
/// O código ausente e o desconhecido são recusas distintas, e a mensagem do desconhecido lista os códigos aceitos sem
/// repetir o valor recebido.
/// </summary>
public static class TipoDeCampoDaEntrada
{
    /// <summary>O tipo do código informado, ou a recusa de domínio quando ele falta ou não é um dos aceitos.</summary>
    public static Result<TipoDeCampo> Ler(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<TipoDeCampo>.Failure(new DomainError(
                ItemFormularioErrorCodes.TipoDeCampoObrigatorio, "O tipo de campo do item é obrigatório."));
        }

        return CatalogoDe<TipoDeCampo>.TentarObter(codigo, out TipoDeCampo tipo)
            ? Result<TipoDeCampo>.Success(tipo)
            : Result<TipoDeCampo>.Failure(new DomainError(
                ItemFormularioErrorCodes.TipoDeCampoInvalido,
                $"O tipo de campo do item não é reconhecido. Os tipos aceitos são {string.Join(", ", CatalogoDe<TipoDeCampo>.Todos().Select(static d => d.Codigo))}."));
    }
}
