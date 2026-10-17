namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="DocumentosAPublicarController.IniciarEnvio"/>: o nome com que a pessoa recebe
/// o arquivo no download e o tipo de mídia dele — <c>application/pdf</c>,
/// <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> (DOCX) ou
/// <c>application/vnd.oasis.opendocument.text</c> (ODT).
/// </summary>
public sealed record IniciarEnvioDoDocumentoAPublicarRequest(string? NomeArquivo, string? TipoDeMidia);
