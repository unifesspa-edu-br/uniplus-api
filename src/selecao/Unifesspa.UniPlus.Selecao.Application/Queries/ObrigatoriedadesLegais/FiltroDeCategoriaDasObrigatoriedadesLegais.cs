namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lê o filtro de categoria da listagem: sem categoria a consulta não filtra, e o código que o enum não conhece é recusado
/// sem repetir o valor recebido.
/// </summary>
public static class FiltroDeCategoriaDasObrigatoriedadesLegais
{
    public const string CodigoDeErro = "ObrigatoriedadeLegal.FiltroDeCategoriaInvalido";

    public static Result<CategoriaObrigatoriedade?> Ler(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
        {
            return Result<CategoriaObrigatoriedade?>.Success(null);
        }

        return CatalogoDe<CategoriaObrigatoriedade>.TentarObter(categoria, out CategoriaObrigatoriedade valor)
            ? Result<CategoriaObrigatoriedade?>.Success(valor)
            : Result<CategoriaObrigatoriedade?>.Failure(new DomainError(
                CodigoDeErro,
                "Categoria inválida. Use um dos códigos publicados para a categoria da obrigatoriedade."));
    }
}
