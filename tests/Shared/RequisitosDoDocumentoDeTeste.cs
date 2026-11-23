namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os requisitos da exigência de documento para os testes que não discutem o prazo de emissão nem a validade:
/// a publicação exige a escolha dos dois, e aqui ela é a mais simples, sem prazo e sem exigir documento válido.
/// </summary>
internal static class RequisitosDoDocumentoDeTeste
{
    /// <summary>O mesmo conteúdo dos requisitos de <see cref="Com"/>, como o corpo do pedido que define os documentos exigidos.</summary>
    public static object QualquerFormatoEmJson { get; } = new
    {
        formatosPermitidos = "QUALQUER",
        prazoDeEmissao = new { escolha = "SEM_PRAZO", valor = (int?)null, unidade = (string?)null, referencia = (object?)null, efeitoForaDoPrazo = (string?)null },
        validade = new { escolha = "NAO_EXIGE_DOCUMENTO_VALIDO", referencia = (object?)null, efeitoDoVencimento = (string?)null },
    };

    /// <summary>Os requisitos com os formatos dados, sem prazo de emissão e sem exigir documento válido.</summary>
    public static RequisitosDoDocumento Com(FormatosPermitidos formatos, int? tamanhoMaximoBytes = null, ModeloDaExigencia? modelo = null) =>
        new(formatos, tamanhoMaximoBytes, modelo,
            new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir());
}
