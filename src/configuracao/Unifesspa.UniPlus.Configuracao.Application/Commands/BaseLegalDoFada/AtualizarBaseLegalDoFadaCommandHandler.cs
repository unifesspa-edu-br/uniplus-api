namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AtualizarBaseLegalDoFadaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarBaseLegalDoFadaCommand command,
        IBaseLegalDoFadaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        BaseLegalDoFada? entity = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        return entity is null
            ? Result.Failure(RecusasDaBaseLegalDoFada.NaoEncontrada())
            : await AtualizarAsync(entity, command, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarAsync(
        BaseLegalDoFada entity,
        AtualizarBaseLegalDoFadaCommand command,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result atualizacao = entity.Atualizar(command.TipoInstrumento, command.Identificacao, command.Descricao, CriarMunicipioDaBaseLegalDoFadaCommand.ParaABaseLegal(command.Municipios));

        if (atualizacao.IsFailure)
        {
            return Result.ValidationFailure(atualizacao.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
