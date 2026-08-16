namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarFaseCanonicaCommand"/>. Valida antes de I/O só o
/// que é determinável sem o código persistido (os sete campos comuns, incluindo a
/// coerência que exige produzir resultado quando o resultado é definitivo) — as
/// duas coerências que dependem do código (<c>AgrupaEtapas</c>,
/// <c>PermiteComplementacao</c>) só podem ser avaliadas depois do fetch, porque o
/// comando não carrega o código (imutável). Carrega a fase (404), então chama
/// <c>Atualizar</c>, que revalida o lote inteiro — inclusive as duas coerências
/// dependentes — contra o código congelado. Sem integridade referencial — a fase
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

        Result<(string Nome, string? Descricao, DonoTipico DonoTipico, string? BaseLegal,
            bool ProduzResultado, bool ResultadoDefinitivo, OrigemDataFase OrigemData)> comuns =
            FaseCanonica.ValidarCamposComuns(
                command.Nome, command.Descricao, command.DonoTipico, command.BaseLegal,
                command.ProduzResultado, command.ResultadoDefinitivo, command.OrigemData);
        if (comuns.IsFailure)
        {
            return Result.ValidationFailure(comuns.Errors);
        }

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
