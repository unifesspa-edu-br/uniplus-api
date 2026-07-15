namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarFaseCanonicaCommand"/>. Carrega a fase (404 se
/// inexistente), aplica os campos editáveis (o <c>Codigo</c> é imutável, então não
/// há checagem de unicidade nem corrida de índice) e revalida as invariantes de
/// coerência (422) contra o código congelado. Sem integridade referencial — a fase
/// não é referenciada por FK intra-banco.
/// </summary>
public static class AtualizarFaseCanonicaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarFaseCanonicaCommand command,
        IFaseCanonicaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        FaseCanonica? fase = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (fase is null)
        {
            return Result.Failure(new DomainError(
                FaseCanonicaErrorCodes.NaoEncontrada,
                "Fase canônica não encontrada."));
        }

        Result atualizarResult = fase.Atualizar(
            command.Nome,
            command.Descricao,
            command.DonoTipico,
            command.AgrupaEtapas,
            command.PermiteComplementacao,
            command.BaseLegal,
            command.ProduzResultado,
            command.ResultadoDefinitivo,
            command.ColetaInscricao,
            command.OrigemData);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
