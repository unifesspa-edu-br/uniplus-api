namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterCalendarioDiasUteisPorIdQueryHandler
{
    public static async Task<Result<CalendarioDiasUteisDto>> Handle(
        ObterCalendarioDiasUteisPorIdQuery query,
        ICalendarioDiasUteisRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        CalendarioDiasUteis? calendario = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return calendario is null
            ? Result<CalendarioDiasUteisDto>.Failure(new DomainError(CalendarioDiasUteisErrorCodes.NaoEncontrado, "Calendário de dias úteis não encontrado."))
            : Result<CalendarioDiasUteisDto>.Success(calendario.ToDto());
    }
}
