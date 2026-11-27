namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.ComponentModel.DataAnnotations;

internal sealed record ItemDoCorpo(DocumentoDoCorpo Documento, string Orgao, string? Observacao = null);

internal sealed record DocumentoDoCorpo(AplicabilidadeDoCorpo Aplicabilidade, string Titulo);

internal sealed record AplicabilidadeDoCorpo(IReadOnlyList<CondicaoDoCorpo> Condicoes);

internal sealed record CondicaoDoCorpo(
    string Fato,
    [property: StringLength(5)] string Limitada,
    [property: Required(ErrorMessage = "A mensagem própria do contrato.")] string ComMensagem);
