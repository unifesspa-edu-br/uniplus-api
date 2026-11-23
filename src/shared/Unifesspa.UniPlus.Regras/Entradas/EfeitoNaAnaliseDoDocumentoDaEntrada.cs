namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public static class EfeitoNaAnaliseDoDocumentoDaEntrada
{
    public static Result<EfeitoNaAnaliseDoDocumento> Ler(string? codigo) =>
        codigo is not null && CatalogoDe<EfeitoNaAnaliseDoDocumento>.TentarObter(codigo, out EfeitoNaAnaliseDoDocumento valor)
            ? Result<EfeitoNaAnaliseDoDocumento>.Success(valor)
            : Result<EfeitoNaAnaliseDoDocumento>.Failure(new DomainError(EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido,
                $"O efeito na análise do documento não é reconhecido. Os códigos aceitos são {string.Join(", ", CatalogoDe<EfeitoNaAnaliseDoDocumento>.Todos().Select(static item => item.Codigo))}."));
}
