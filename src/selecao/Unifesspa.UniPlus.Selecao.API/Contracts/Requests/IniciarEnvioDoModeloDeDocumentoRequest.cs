namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Corpo de <see cref="ModelosDeDocumentoController.IniciarEnvio"/>: o nome que o candidato
/// recebe no download e o formato editável do arquivo, <c>DOCX</c> ou <c>ODT</c>.
/// </summary>
public sealed record IniciarEnvioDoModeloDeDocumentoRequest(string? NomeArquivo, [property: AceitaCodigosDe<FormatoDeDocumento>] string? Formato);
