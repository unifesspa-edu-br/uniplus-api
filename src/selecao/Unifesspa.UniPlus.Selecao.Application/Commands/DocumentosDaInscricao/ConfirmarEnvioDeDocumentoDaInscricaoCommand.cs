namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 3 do envio: o candidato confirma o PUT com o ETag que ele devolveu; a API põe o arquivo em verificação.
/// </summary>
public sealed record ConfirmarEnvioDeDocumentoDaInscricaoCommand(
    Guid ProcessoSeletivoId, Guid ArquivoId, string ETag) : ICommand<Result<ArquivoDaApresentacaoDto>>;
