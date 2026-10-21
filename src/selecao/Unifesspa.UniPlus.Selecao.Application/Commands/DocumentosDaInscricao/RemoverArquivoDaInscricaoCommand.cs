namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>Remove o arquivo do rascunho do dono do token, apagando as duas chaves do armazenamento.</summary>
public sealed record RemoverArquivoDaInscricaoCommand(Guid ProcessoSeletivoId, Guid ArquivoId) : ICommand<Result>;
