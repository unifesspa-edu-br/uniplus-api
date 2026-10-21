namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 3 do envio: lê o arquivo do armazenamento, confere o conteúdo contra o formato, passa o
/// antivírus, calcula o SHA-256 e sela o conteúdo cifrado.
/// </summary>
public sealed record ConfirmarEnvioDeDocumentoDaInscricaoCommand(
    Guid ProcessoSeletivoId, Guid ArquivoId) : ICommand<Result<ArquivoDaApresentacaoDto>>;
