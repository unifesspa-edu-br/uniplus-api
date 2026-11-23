namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public static class UnidadeDoPrazoDeEmissaoDaEntrada
{
    public static Result<UnidadeDoPrazoDeEmissao> Ler(string? codigo) =>
        codigo is not null && CatalogoDe<UnidadeDoPrazoDeEmissao>.TentarObter(codigo, out UnidadeDoPrazoDeEmissao valor)
            ? Result<UnidadeDoPrazoDeEmissao>.Success(valor)
            : Result<UnidadeDoPrazoDeEmissao>.Failure(new DomainError(PrazoDeEmissaoErrorCodes.UnidadeInvalida,
                $"O unidade do prazo de emissão não é reconhecido. Os códigos aceitos são {string.Join(", ", CatalogoDe<UnidadeDoPrazoDeEmissao>.Todos().Select(static item => item.Codigo))}."));
}
