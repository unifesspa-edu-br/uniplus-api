namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

internal static class ErrosDosDocumentos
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(PrazoDeEmissaoErrorCodes.EscolhaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prazo_de_emissao.escolha_invalida", "A escolha do prazo de emissão não é reconhecida")),
        new(PrazoDeEmissaoErrorCodes.UnidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prazo_de_emissao.unidade_invalida", "A unidade do prazo de emissão não é reconhecida")),
        new(ValidadeDoDocumentoErrorCodes.EscolhaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.validade_do_documento.escolha_invalida", "A escolha da validade não é reconhecida")),
        new(DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.data_de_referencia_do_documento.tipo_invalido", "O tipo da data de referência não é reconhecido")),
        new(EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.efeito_na_analise_do_documento.invalido", "O efeito na análise do documento não é reconhecido")),
    ];
}
