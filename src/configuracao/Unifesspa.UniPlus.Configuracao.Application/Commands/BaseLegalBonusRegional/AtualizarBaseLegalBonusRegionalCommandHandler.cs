namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AtualizarBaseLegalBonusRegionalCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarBaseLegalBonusRegionalCommand command,
        IBaseLegalBonusRegionalRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        BaseLegalBonusRegional? entity = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure(new DomainError(BaseLegalBonusRegionalErrorCodes.NaoEncontrado, "Base legal de bônus regional não encontrada."));
        }

        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipiosTuples = command.Municipios?.Select(m => (m.CodigoIbge, m.Nome, m.Uf));

        Result atualizacaoResult = entity.Atualizar(
            command.TipoInstrumento,
            command.Identificacao,
            command.Descricao,
            municipiosTuples);

        if (atualizacaoResult.IsFailure)
        {
            return Result.ValidationFailure(atualizacaoResult.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
