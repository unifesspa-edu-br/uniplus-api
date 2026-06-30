namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) um tipo de documento pelo seu <c>Id</c>.</summary>
public sealed record RemoverTipoDocumentoCommand(Guid Id) : ICommand<Result>;
