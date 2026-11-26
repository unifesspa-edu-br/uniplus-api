namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
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
        if (entity is null)
        {
            return Result.Failure(new DomainError(BaseLegalDoFadaErrorCodes.NaoEncontrado, "Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada."));
        }

        // m pode ser null (JSON "municipios": [null]) — ver o mesmo comentário em
        // CriarBaseLegalDoFadaCommandHandler.
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipiosTuples =
            command.Municipios?.Select(m => (m?.CodigoIbge, m?.Nome, m?.Uf));

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
