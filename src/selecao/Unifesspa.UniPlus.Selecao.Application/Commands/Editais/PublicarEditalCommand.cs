namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Kernel.Results;

/// <summary>
/// Comando para publicar um edital existente. Após a publicação,
/// <see cref="Domain.Entities.Edital.Publicar"/> emite o
/// <see cref="Domain.Events.EditalPublicadoEvent"/>, drenado por cascading
/// messages no handler (ADR-0005).
/// </summary>
public sealed record PublicarEditalCommand(Guid EditalId) : ICommand<Result>;
