namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarReferenciaReservaDemograficaCommand"/>
/// (convention-based Wolverine): valida o payload primeiro (422, sem I/O —
/// validação sempre vence I/O), confere a unicidade do Censo entre referências
/// vivas (409, DB), persiste e commita.
/// </summary>
public static class CriarReferenciaReservaDemograficaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarReferenciaReservaDemograficaCommand command,
        IReferenciaReservaDemograficaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ReferenciaReservaDemografica> referenciaResult = ReferenciaReservaDemografica.Criar(
            command.CensoReferencia,
            command.PpiPercentual,
            command.QuilombolaPercentual,
            command.PcdPercentual,
            command.BaseLegal);

        return referenciaResult.Value is not { } referencia
            ? Result<Guid>.ValidationFailure(referenciaResult.Errors)
            : await IncluirAsync(referencia, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> IncluirAsync(
        ReferenciaReservaDemografica referencia,
        IReferenciaReservaDemograficaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result censo = await CensoUnicoDaReferencia
            .ConferirAsync(referencia.CensoReferencia, null, repository, cancellationToken).ConfigureAwait(false);
        if (censo.Error is { } censoRecusado)
        {
            return Result<Guid>.Failure(censoRecusado);
        }

        await repository.AdicionarAsync(referencia, cancellationToken).ConfigureAwait(false);
        Result gravado = await CensoUnicoDaReferencia.GravarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);

        return gravado.Error is { } gravacaoRecusada
            ? Result<Guid>.Failure(gravacaoRecusada)
            : Result<Guid>.Success(referencia.Id);
    }
}
