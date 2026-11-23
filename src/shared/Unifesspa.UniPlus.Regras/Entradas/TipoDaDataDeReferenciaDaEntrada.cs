namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public static class TipoDaDataDeReferenciaDaEntrada
{
    public static Result<TipoDaDataDeReferencia> Ler(string? codigo) =>
        codigo is not null && CatalogoDe<TipoDaDataDeReferencia>.TentarObter(codigo, out TipoDaDataDeReferencia valor)
            ? Result<TipoDaDataDeReferencia>.Success(valor)
            : Result<TipoDaDataDeReferencia>.Failure(new DomainError(DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido,
                $"O tipo de data de referência não é reconhecido. Os códigos aceitos são {string.Join(", ", CatalogoDe<TipoDaDataDeReferencia>.Todos().Select(static item => item.Codigo))}."));
}
