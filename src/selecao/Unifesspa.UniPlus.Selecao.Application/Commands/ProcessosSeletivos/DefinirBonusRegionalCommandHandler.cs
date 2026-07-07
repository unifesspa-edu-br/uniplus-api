namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirBonusRegionalCommand"/> (RN05, Story #774):
/// <c>RegraCodigo</c> nulo remove o bônus (toggle por ausência, INV-B5); caso
/// contrário resolve a regra <c>BONUS-MULTIPLICATIVO</c> no
/// <c>rol_de_regras</c> (<see cref="IRegraCatalogoReader"/>, Story #772).
/// </summary>
public static class DefinirBonusRegionalCommandHandler
{
    public static async Task<Result> Handle(
        DefinirBonusRegionalCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        if (command.RegraCodigo is null)
        {
            processo.DefinirBonusRegional(null);
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        if (command.RegraVersao is null || command.Fator is null)
        {
            return Result.Failure(new DomainError(
                "ConfiguracaoBonusRegional.CamposObrigatorios",
                "RegraVersao e Fator são obrigatórios quando RegraCodigo é informado."));
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(command.RegraCodigo, command.RegraVersao, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(new DomainError(
                "ConfiguracaoBonusRegional.RegraNaoEncontrada",
                $"Regra de bônus {command.RegraCodigo}/{command.RegraVersao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != TipoRegra.RegraBonus)
        {
            return Result.Failure(new DomainError(
                "ConfiguracaoBonusRegional.RegraTipoInvalido",
                $"A regra {command.RegraCodigo}/{command.RegraVersao} não é do tipo regra_bonus."));
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return Result.Failure(referenciaRegraResult.Error!);
        }

        Result<ConfiguracaoBonusRegional> bonusResult = ConfiguracaoBonusRegional.Criar(
            referenciaRegraResult.Value!, command.Fator.Value, command.Teto, command.MunicipioConvenio, command.BaseLegal);
        if (bonusResult.IsFailure)
        {
            return Result.Failure(bonusResult.Error!);
        }

        processo.DefinirBonusRegional(bonusResult.Value!);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
