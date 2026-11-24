namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;

using Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lê o filtro de finalidade da listagem: sem finalidade a consulta não filtra, e o código que o enum não conhece é
/// recusado sem repetir o valor recebido.
/// </summary>
public static class FiltroDeFinalidadeDosDocumentosAPublicar
{
    public const string CodigoDeErro = "DocumentoAPublicar.FiltroDeFinalidadeInvalido";

    public static Result<FinalidadeDoDocumento?> Ler(string? finalidade)
    {
        if (string.IsNullOrWhiteSpace(finalidade))
        {
            return Result<FinalidadeDoDocumento?>.Success(null);
        }

        return CatalogoDe<FinalidadeDoDocumento>.TentarObter(finalidade, out FinalidadeDoDocumento valor)
            ? Result<FinalidadeDoDocumento?>.Success(valor)
            : Result<FinalidadeDoDocumento?>.Failure(new DomainError(
                CodigoDeErro,
                "Finalidade inválida. Use um dos códigos publicados para a finalidade do documento."));
    }
}
