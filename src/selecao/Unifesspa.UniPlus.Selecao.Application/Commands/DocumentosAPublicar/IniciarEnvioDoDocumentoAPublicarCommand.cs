namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 1 do envio de um documento que o processo vai publicar: cria o registro pendente com o
/// nome e o tipo de mídia e devolve a URL pré-assinada de PUT — o arquivo não trafega pela API.
/// </summary>
/// <param name="NomeArquivo">O nome com que a pessoa recebe o arquivo no download.</param>
/// <param name="TipoDeMidia"><c>application/pdf</c>, DOCX ou ODT.</param>
public sealed record IniciarEnvioDoDocumentoAPublicarCommand(
    Guid ProcessoSeletivoId, string? NomeArquivo, string? TipoDeMidia) : ICommand<Result<IniciarEnvioDoDocumentoAPublicarDto>>;
