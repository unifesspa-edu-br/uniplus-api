namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

using Domain.Enums;

/// <summary>
/// Corpo de <see cref="DocumentosAPublicarController.IniciarEnvio"/>: para que o documento é
/// enviado (<c>documentoDoEdital</c>, que só admite PDF, <c>anexoDoEdital</c> ou <c>documentoDeAto</c>), o nome
/// com que a pessoa recebe o arquivo no download e o tipo de mídia dele — <c>application/pdf</c>,
/// <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> (DOCX) ou
/// <c>application/vnd.oasis.opendocument.text</c> (ODT).
/// </summary>
public sealed record IniciarEnvioDoDocumentoAPublicarRequest(FinalidadeDoDocumento? Finalidade, string? NomeArquivo, string? TipoDeMidia);
