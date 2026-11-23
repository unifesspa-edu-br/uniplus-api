namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public static class EscolhaDaValidadeDaEntrada
{
    public static Result<EscolhaDaValidade> Ler(string? codigo) =>
        codigo is not null && CatalogoDe<EscolhaDaValidade>.TentarObter(codigo, out EscolhaDaValidade valor)
            ? Result<EscolhaDaValidade>.Success(valor)
            : Result<EscolhaDaValidade>.Failure(new DomainError(ValidadeDoDocumentoErrorCodes.EscolhaInvalida,
                $"O escolha da validade não é reconhecido. Os códigos aceitos são {string.Join(", ", CatalogoDe<EscolhaDaValidade>.Todos().Select(static item => item.Codigo))}."));
}
