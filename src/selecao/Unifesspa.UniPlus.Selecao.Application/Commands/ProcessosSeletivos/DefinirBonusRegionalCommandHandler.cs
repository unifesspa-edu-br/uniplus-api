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

        Result<ProcessoSeletivo> obtido = await ObterParaEdicaoAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirAsync(command, obtido.Value!, regraCatalogoReader, baseLegalBonusRegionalReader, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    // Fora do rascunho a recusa vem antes de consultar cadastros.
    internal static async Task<Result<ProcessoSeletivo>> ObterParaEdicaoAsync(
        IProcessoSeletivoRepository repositorio, Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio.ObterParaMutacaoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(processoSeletivoId));
        }

        return processo.MutacaoBloqueada() is { } bloqueio
            ? Result<ProcessoSeletivo>.Failure(bloqueio)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<Result> DefinirAsync(
        DefinirBonusRegionalCommand command,
        ProcessoSeletivo processo,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalBonusRegionalReader baseLegalBonusRegionalReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken) =>
        command.Aplica
            ? await AplicarAsync(command, processo, regraCatalogoReader, baseLegalBonusRegionalReader, unitOfWork, cancellationToken)
                .ConfigureAwait(false)
            : await NaoAplicarAsync(command, processo, unitOfWork, cancellationToken).ConfigureAwait(false);

    private static async Task<Result> NaoAplicarAsync(
        DefinirBonusRegionalCommand command,
        ProcessoSeletivo processo,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (command.TemConfiguracaoDoBonus)
        {
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao,
                "O processo que não aplica o bônus regional não pode ter a configuração do bônus."));
        }

        return await DefinirESalvarAsync(processo, aplica: false, bonus: null, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AplicarAsync(
        DefinirBonusRegionalCommand command,
        ProcessoSeletivo processo,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalBonusRegionalReader baseLegalBonusRegionalReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ConfiguracaoDeclarada? declarada = ConfiguracaoDeclarada.De(command);
        if (declarada is null)
        {
            return Result.Failure(RecusaDaDeclaracaoIncompleta(command));
        }

        Result<ConfiguracaoBonusRegional> bonus = await ResolverAsync(declarada, regraCatalogoReader, baseLegalBonusRegionalReader, cancellationToken)
            .ConfigureAwait(false);
        if (bonus.IsFailure)
        {
            return Result.ValidationFailure(bonus.Errors);
        }

        return await DefinirESalvarAsync(processo, aplica: true, bonus.Value, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirESalvarAsync(
        ProcessoSeletivo processo, bool aplica, ConfiguracaoBonusRegional? bonus, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result definido = processo.DefinirBonusRegional(aplica, bonus);
        if (definido.IsFailure)
        {
            return Result.Failure(definido.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static async Task<Result<ConfiguracaoBonusRegional>> ResolverAsync(
        ConfiguracaoDeclarada declarada,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalBonusRegionalReader baseLegalBonusRegionalReader,
        CancellationToken cancellationToken)
    {
        Result<ReferenciaRegra> regra = await ResolverRegraAsync(declarada, regraCatalogoReader, cancellationToken).ConfigureAwait(false);
        if (regra.IsFailure)
        {
            return Result<ConfiguracaoBonusRegional>.Failure(regra.Error!);
        }

        Result<BaseLegalCongelada> baseLegal = await ResolverBaseLegalAsync(declarada.BaseLegalId, baseLegalBonusRegionalReader, cancellationToken)
            .ConfigureAwait(false);
        if (baseLegal.IsFailure)
        {
            return Result<ConfiguracaoBonusRegional>.Failure(baseLegal.Error!);
        }

        return ConfiguracaoBonusRegional.Criar(regra.Value!, declarada.Fator, declarada.Teto, declarada.FatoElegibilidadeCodigo, baseLegal.Value!);
    }

    private static async Task<Result<ReferenciaRegra>> ResolverRegraAsync(
        ConfiguracaoDeclarada declarada, IRegraCatalogoReader regraCatalogoReader, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(declarada.RegraCodigo, declarada.RegraVersao, cancellationToken)
            .ConfigureAwait(false);

        return regra switch
        {
            null => Falha<ReferenciaRegra>(
                "ConfiguracaoBonusRegional.RegraNaoEncontrada",
                $"Regra de bônus {declarada.RegraCodigo}/{declarada.RegraVersao} não encontrada no catálogo de regras."),
            { Tipo: not TipoRegra.RegraBonus } => Falha<ReferenciaRegra>(
                "ConfiguracaoBonusRegional.RegraTipoInvalido",
                $"A regra {declarada.RegraCodigo}/{declarada.RegraVersao} não é do tipo regra_bonus."),
            _ => Result<ReferenciaRegra>.Success(regra.Referencia()),
        };
    }

    /// <summary>
    /// Reader cross-módulo (ADR-0056): o filtro global de soft-delete de Configuração já faz um Id inexistente e um
    /// desativado colapsarem no mesmo null — CA-02 não precisa distinguir os dois casos.
    /// </summary>
    private static async Task<Result<BaseLegalCongelada>> ResolverBaseLegalAsync(
        Guid baseLegalId, IBaseLegalBonusRegionalReader baseLegalBonusRegionalReader, CancellationToken cancellationToken)
    {
        BaseLegalBonusRegionalView? baseLegal = await baseLegalBonusRegionalReader
            .ObterPorIdAsync(baseLegalId, cancellationToken)
            .ConfigureAwait(false);

        return baseLegal is null
            ? Falha<BaseLegalCongelada>(
                "ConfiguracaoBonusRegional.BaseLegalNaoEncontrada",
                $"Base legal de bônus regional {baseLegalId} não encontrada ou desativada.")
            : Result<BaseLegalCongelada>.Success(Congelar(baseLegal));
    }

    private static DomainError RecusaDaDeclaracaoIncompleta(DefinirBonusRegionalCommand command) =>
        command.Regra?.Codigo is null
            ? new(
                ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao,
                "O processo que aplica o bônus regional precisa da configuração do bônus.")
            : new(
                "ConfiguracaoBonusRegional.CamposObrigatorios",
                "A versão da regra, o fator e a base legal são obrigatórios quando o código da regra é informado.");

    private static Result<T> Falha<T>(string codigo, string mensagem) => Result<T>.Failure(new DomainError(codigo, mensagem));

    private static BaseLegalCongelada Congelar(BaseLegalBonusRegionalView baseLegal) =>
        new(
            baseLegal.Id,
            baseLegal.Codigo,
            baseLegal.TipoInstrumento,
            baseLegal.Identificacao,
            baseLegal.Descricao,
            [.. baseLegal.Municipios.Select(static m => (m.CodigoIbge, m.Nome, m.Uf))]);

    /// <summary>A configuração do bônus com todos os campos que o processo que o aplica precisa declarar.</summary>
    private sealed record ConfiguracaoDeclarada(
        string RegraCodigo,
        string RegraVersao,
        decimal Fator,
        decimal? Teto,
        string? FatoElegibilidadeCodigo,
        Guid BaseLegalId)
    {
        public static ConfiguracaoDeclarada? De(DefinirBonusRegionalCommand command) =>
            command is { Regra: { Codigo: { } codigo, Versao: { } versao }, Fator: { } fator, BaseLegal: { } baseLegal }
                ? new(codigo, versao, fator, command.Teto, command.FatoElegibilidadeCodigo, baseLegal.Id)
                : null;
    }
}
