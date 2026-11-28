namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Um anexo do edital: o documento a publicar já confirmado e o nome com que o arquivo é baixado.
/// </summary>
/// <param name="DocumentoId">O documento a publicar confirmado, do mesmo processo.</param>
/// <param name="Nome">
/// Nome do arquivo no download. A extensão do formato é acrescentada quando falta; o nome é
/// obrigatório, tem até 120 caracteres com a extensão e não admite separador de caminho.
/// </param>
public sealed record AnexoDoEditalRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    Guid? DocumentoId,
    string? Nome);
