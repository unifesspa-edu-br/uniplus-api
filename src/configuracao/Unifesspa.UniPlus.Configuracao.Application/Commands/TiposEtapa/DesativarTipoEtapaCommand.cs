namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record DesativarTipoEtapaCommand(Guid Id) : ICommand<Result>;
