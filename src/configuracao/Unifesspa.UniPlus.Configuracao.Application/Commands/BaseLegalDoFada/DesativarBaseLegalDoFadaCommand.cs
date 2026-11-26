namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record DesativarBaseLegalDoFadaCommand(Guid Id) : ICommand<Result>;
