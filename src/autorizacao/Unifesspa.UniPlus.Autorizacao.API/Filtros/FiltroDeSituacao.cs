namespace Unifesspa.UniPlus.Autorizacao.API.Filtros;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lê um filtro opcional de situação pelo código de contrato exato.</summary>
public static class FiltroDeSituacao
{
    public static Result<TEnum?> Ler<TEnum>(string? texto, DomainError recusa)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(texto))
        {
            return Result<TEnum?>.Success(null);
        }

        return CatalogoDe<TEnum>.TentarObter(texto, out TEnum valor)
            ? Result<TEnum?>.Success(valor)
            : Result<TEnum?>.Failure(recusa);
    }
}
