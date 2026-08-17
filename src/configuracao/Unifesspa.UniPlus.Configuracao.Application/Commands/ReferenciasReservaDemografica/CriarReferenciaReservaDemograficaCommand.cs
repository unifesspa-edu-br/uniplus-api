namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma Referência de reserva demográfica: um Censo com os três percentuais
/// demográficos (PPI, quilombola, PcD) e a base legal. Os atores de auditoria
/// (<c>created_by</c>) são carimbados server-side via <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>CensoReferencia</c> e <c>BaseLegal</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem valor default, para o schema OpenAPI continuar listando-os
/// como obrigatórios; nulos, para o campo ausente escapar do <c>[ApiController]</c>
/// e chegar à validação de domínio.
/// </remarks>
public sealed record CriarReferenciaReservaDemograficaCommand(
    string? CensoReferencia,
    decimal PpiPercentual,
    decimal QuilombolaPercentual,
    decimal PcdPercentual,
    string? BaseLegal) : ICommand<Result<Guid>>;
