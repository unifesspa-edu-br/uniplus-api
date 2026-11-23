namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public static class EscolhaDoPrazoDeEmissaoDaEntrada
{
    public static Result<EscolhaDoPrazoDeEmissao> Ler(string? codigo) =>
        codigo is not null && CatalogoDe<EscolhaDoPrazoDeEmissao>.TentarObter(codigo, out EscolhaDoPrazoDeEmissao valor)
            ? Result<EscolhaDoPrazoDeEmissao>.Success(valor)
            : Result<EscolhaDoPrazoDeEmissao>.Failure(new DomainError(PrazoDeEmissaoErrorCodes.EscolhaInvalida,
                $"O escolha do prazo de emissão não é reconhecido. Os códigos aceitos são {string.Join(", ", CatalogoDe<EscolhaDoPrazoDeEmissao>.Todos().Select(static item => item.Codigo))}."));
}
