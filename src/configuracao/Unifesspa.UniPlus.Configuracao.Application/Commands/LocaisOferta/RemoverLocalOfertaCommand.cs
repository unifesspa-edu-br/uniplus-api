namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record RemoverLocalOfertaCommand(Guid Id) : ICommand<Result>;
