namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record AtualizarReferenciaReservaDemograficaCommand(
    Guid Id,
    string CensoReferencia,
    decimal PpiPercentual,
    decimal QuilombolaPercentual,
    decimal PcdPercentual,
    string BaseLegal) : ICommand<Result>;
