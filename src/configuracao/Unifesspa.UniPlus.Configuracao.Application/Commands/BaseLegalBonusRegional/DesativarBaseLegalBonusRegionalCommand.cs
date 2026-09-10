namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record DesativarBaseLegalBonusRegionalCommand(Guid Id) : ICommand<Result>;
