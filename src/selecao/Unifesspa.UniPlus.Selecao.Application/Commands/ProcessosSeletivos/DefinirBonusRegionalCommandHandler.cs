namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirBonusRegionalCommand"/> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015, Story #774):
/// <c>Aplica</c> falso declara que o processo não aplica o bônus; verdadeiro resolve a regra <c>BONUS-MULTIPLICATIVO</c> no
/// <c>regras_do_catalogo</c> (<see cref="IRegraCatalogoReader"/>, Story #772) e a Base Legal
/// referenciada (<see cref="IBaseLegalBonusRegionalReader"/>, Configuração, Story #1465),
/// cujo snapshot é congelado em <see cref="ConfiguracaoBonusRegional"/> (Story #1466).
/// </summary>
/// <remarks>
/// Sem <c>[NonTransactional]</c>, de propósito: o <c>SELECT ... FOR UPDATE</c> de
/// <c>ObterParaMutacaoAsync</c> só serializa handlers concorrentes dentro da transação ambiente do
/// Wolverine (ADR-0004), e desligá-la destravaria o lock pessimista.
/// </remarks>
public static class DefinirBonusRegionalCommandHandler
{
    public static async Task<Result> Handle(
        DefinirBonusRegionalCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalBonusRegionalReader baseLegalBonusRegionalReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(baseLegalBonusRegionalReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // Fora do rascunho a recusa vem antes de consultar cadastros.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        return command.Aplica
            ? await AplicarAsync(command, processo, regraCatalogoReader, baseLegalBonusRegionalReader, unitOfWork, cancellationToken)
                .ConfigureAwait(false)
            : await NaoAplicarAsync(command, processo, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> NaoAplicarAsync(
        DefinirBonusRegionalCommand command,
        ProcessoSeletivo processo,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        // Qualquer campo do bônus enviado contradiz a declaração: descartá-lo em silêncio
        // esconderia um payload mal formado.
        if (command.RegraCodigo is not null || command.RegraVersao is not null || command.Fator is not null
            || command.Teto is not null || command.FatoElegibilidadeCodigo is not null || command.BaseLegalBonusRegionalId is not null)
        {
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao,
                "O processo que não aplica o bônus regional não pode ter a configuração do bônus."));
        }

        Result naoAplicaResult = processo.DefinirBonusRegional(aplica: false, bonus: null);
        if (naoAplicaResult.IsFailure)
        {
            return Result.Failure(naoAplicaResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static async Task<Result> AplicarAsync(
        DefinirBonusRegionalCommand command,
        ProcessoSeletivo processo,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalBonusRegionalReader baseLegalBonusRegionalReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (command.RegraCodigo is null)
        {
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao,
                "O processo que aplica o bônus regional precisa da configuração do bônus."));
        }

        if (command.RegraVersao is null || command.Fator is null || command.BaseLegalBonusRegionalId is null)
        {
            return Result.Failure(new DomainError(
                "ConfiguracaoBonusRegional.CamposObrigatorios",
                "RegraVersao, Fator e BaseLegalBonusRegionalId são obrigatórios quando RegraCodigo é informado."));
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(command.RegraCodigo, command.RegraVersao, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(new DomainError(
                "ConfiguracaoBonusRegional.RegraNaoEncontrada",
                $"Regra de bônus {command.RegraCodigo}/{command.RegraVersao} não encontrada no catálogo de regras."));
        }

        if (regra.Tipo != TipoRegra.RegraBonus)
        {
            return Result.Failure(new DomainError(
                "ConfiguracaoBonusRegional.RegraTipoInvalido",
                $"A regra {command.RegraCodigo}/{command.RegraVersao} não é do tipo regra_bonus."));
        }

        // Reader cross-módulo (ADR-0056): o filtro global de soft-delete de Configuração já
        // faz um Id inexistente e um desativado colapsarem no mesmo null — CA-02 não precisa
        // distinguir os dois casos.
        BaseLegalBonusRegionalView? baseLegal = await baseLegalBonusRegionalReader
            .ObterPorIdAsync(command.BaseLegalBonusRegionalId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (baseLegal is null)
        {
            return Result.Failure(new DomainError(
                "ConfiguracaoBonusRegional.BaseLegalNaoEncontrada",
                $"Base legal de bônus regional {command.BaseLegalBonusRegionalId} não encontrada ou desativada."));
        }

        Result<ConfiguracaoBonusRegional> bonusResult = ConfiguracaoBonusRegional.Criar(
            regra.Referencia(),
            command.Fator.Value,
            command.Teto,
            command.FatoElegibilidadeCodigo,
            Congelar(baseLegal));
        if (bonusResult.IsFailure)
        {
            return Result.ValidationFailure(bonusResult.Errors);
        }

        Result definirResult = processo.DefinirBonusRegional(aplica: true, bonusResult.Value!);
        if (definirResult.IsFailure)
        {
            return Result.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static BaseLegalCongelada Congelar(BaseLegalBonusRegionalView baseLegal) =>
        new(
            baseLegal.Id,
            baseLegal.Codigo,
            baseLegal.TipoInstrumento,
            baseLegal.Identificacao,
            baseLegal.Descricao,
            [.. baseLegal.Municipios.Select(static m => (m.CodigoIbge, m.Nome, m.Uf))]);
}
