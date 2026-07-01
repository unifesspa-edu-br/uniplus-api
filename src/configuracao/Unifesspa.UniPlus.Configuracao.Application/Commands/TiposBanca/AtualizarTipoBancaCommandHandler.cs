namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoBancaCommand"/>. Carrega o tipo de banca (404
/// se inexistente), aplica os campos editáveis (o <c>Codigo</c> é imutável, então
/// não há checagem de unicidade nem corrida de índice) e commita. Sem integridade
/// referencial — o tipo de banca não é referenciado por FK intra-banco.
/// </summary>
public static class AtualizarTipoBancaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoBancaCommand command,
        ITipoBancaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoBanca? banca = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (banca is null)
        {
            return Result.Failure(new DomainError(
                TipoBancaErrorCodes.NaoEncontrado,
                "Tipo de banca não encontrado."));
        }

        Result atualizarResult = banca.Atualizar(
            command.Nome,
            command.FaseTipica,
            command.Descricao);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
