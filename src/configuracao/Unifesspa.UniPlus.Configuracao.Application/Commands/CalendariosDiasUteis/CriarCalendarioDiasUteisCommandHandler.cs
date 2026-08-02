namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarCalendarioDiasUteisCommand"/> (convention-based
/// Wolverine): constrói o agregado (token de abrangência analisado e invariantes
/// de coerência conferidas no próprio <c>CalendarioDiasUteis.Criar</c>), persiste
/// e commita. Sem checagem de unicidade — <c>VersaoDataset</c> não é chave natural.
/// </summary>
public static class CriarCalendarioDiasUteisCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarCalendarioDiasUteisCommand command,
        ICalendarioDiasUteisRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<CalendarioDiasUteis> calendarioResult = CalendarioDiasUteis.Criar(
            command.VersaoDataset,
            [.. command.DiasNaoUteis.Select(d => new DiaNaoUtilCriacao(d.Abrangencia, d.MunicipioIbge, d.Data, d.Descricao, d.Uf))]);

        if (calendarioResult.IsFailure)
        {
            return Result<Guid>.Failure(calendarioResult.Error!);
        }

        CalendarioDiasUteis calendario = calendarioResult.Value!;
        await repository.AdicionarAsync(calendario, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(calendario.Id);
    }
}
