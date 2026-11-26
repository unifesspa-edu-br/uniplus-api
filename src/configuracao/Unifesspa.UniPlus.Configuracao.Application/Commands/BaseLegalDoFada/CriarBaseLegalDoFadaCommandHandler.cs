namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarBaseLegalDoFadaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarBaseLegalDoFadaCommand command,
        IBaseLegalDoFadaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<BaseLegalDoFada> criada = BaseLegalDoFada.Criar(
            command.TipoInstrumento,
            command.Identificacao,
            command.Descricao,
            CriarMunicipioDaBaseLegalDoFadaCommand.ParaABaseLegal(command.Municipios));

        return criada.IsFailure
            ? Result<Guid>.ValidationFailure(criada.Errors)
            : await GravarAsync(criada.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // O código é único entre as bases legais vivas: a conferência prévia dá a recusa comum e o índice único decide a corrida.
    private static async Task<Result<Guid>> GravarAsync(
        BaseLegalDoFada entity,
        IBaseLegalDoFadaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteAsync(entity.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(RecusasDaBaseLegalDoFada.CodigoJaExiste(entity));
        }

        await repository.AdicionarAsync(entity, cancellationToken).ConfigureAwait(false);

        return await SalvarAsync(entity, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> SalvarAsync(BaseLegalDoFada entity, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(entity.Id);
        }
        catch (Exception exception) when (ConstraintVioladaNaGravacao.NomeDaConstraintViolada(exception)
            == RestricoesDoBanco.BaseLegalDoFadaCodigoAtivos)
        {
            // Sem descartar, a inserção continua rastreada e o SaveChanges do outbox a repete fora deste catch (500 em vez de 409).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(RecusasDaBaseLegalDoFada.CodigoJaExiste(entity));
        }
    }
}
