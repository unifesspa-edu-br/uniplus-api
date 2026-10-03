namespace Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 3 do envio do modelo de documento: lê o objeto do storage, valida o conteúdo contra o
/// formato declarado, calcula o SHA-256 no servidor e finaliza o modelo como imutável.
/// </summary>
public sealed record ConfirmarEnvioDoModeloDeDocumentoCommand(
    Guid ProcessoSeletivoId, Guid ModeloDeDocumentoId) : ICommand<Result<ModeloDeDocumentoDto>>;
