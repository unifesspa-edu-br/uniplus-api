namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) uma categoria de documento pelo seu <c>Id</c>.</summary>
public sealed record RemoverCategoriaDocumentoCommand(Guid Id) : ICommand<Result>;
