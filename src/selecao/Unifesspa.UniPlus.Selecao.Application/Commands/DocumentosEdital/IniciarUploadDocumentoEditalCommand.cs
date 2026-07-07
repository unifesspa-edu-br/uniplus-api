namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;

using DTOs;
using Kernel.Results;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 1 do fluxo de upload direto do documento do Edital (Story #759, T3
/// #784): cria o registro pendente vinculado ao processo e devolve a URL
/// pre-assinada de PUT — o arquivo não trafega pela API.
/// </summary>
public sealed record IniciarUploadDocumentoEditalCommand(
    Guid ProcessoSeletivoId) : ICommand<Result<IniciarUploadDocumentoEditalDto>>;
