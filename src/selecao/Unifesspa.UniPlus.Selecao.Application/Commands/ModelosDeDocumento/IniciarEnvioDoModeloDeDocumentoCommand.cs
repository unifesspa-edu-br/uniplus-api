namespace Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 1 do envio do modelo de documento: cria o registro pendente com o nome e o formato e
/// devolve a URL pré-assinada de PUT — o arquivo não trafega pela API.
/// </summary>
/// <param name="NomeArquivo">O nome que o candidato recebe no download.</param>
/// <param name="Formato"><c>DOCX</c> ou <c>ODT</c>.</param>
public sealed record IniciarEnvioDoModeloDeDocumentoCommand(
    Guid ProcessoSeletivoId, string? NomeArquivo, string? Formato) : ICommand<Result<IniciarEnvioDoModeloDeDocumentoDto>>;
