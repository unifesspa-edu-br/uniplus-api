namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

using Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Corpo de <see cref="DocumentosAPublicarController.IniciarEnvio"/>: para que o documento é
/// enviado (<c>DOCUMENTO_DO_EDITAL</c>, que só admite PDF, <c>ANEXO_DO_EDITAL</c> ou <c>DOCUMENTO_DE_ATO</c>), o nome
/// com que a pessoa recebe o arquivo no download e o tipo de mídia dele — <c>application/pdf</c>,
/// <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> (DOCX) ou
/// <c>application/vnd.oasis.opendocument.text</c> (ODT).
/// </summary>
public sealed record IniciarEnvioDoDocumentoAPublicarRequest([property: AceitaCodigosDe<FinalidadeDoDocumento>] string? Finalidade, string? NomeArquivo, string? TipoDeMidia);
