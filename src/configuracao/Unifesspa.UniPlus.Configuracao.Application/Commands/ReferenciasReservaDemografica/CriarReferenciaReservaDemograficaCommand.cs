namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma Referência de reserva demográfica: um Censo com os três percentuais
/// demográficos (PPI, quilombola, PcD) e a base legal. Os atores de auditoria
/// (<c>created_by</c>) são carimbados server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarReferenciaReservaDemograficaCommand(
    string CensoReferencia,
    decimal PpiPercentual,
    decimal QuilombolaPercentual,
    decimal PcdPercentual,
    string BaseLegal) : ICommand<Result<Guid>>;
