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
/// Handler do <see cref="DefinirFadaCommand"/> (Fator de Atenuação das Desigualdades de Acesso — FADA, UNI-REQ-0015):
/// <c>Aplica</c> falso declara que o processo não aplica o FADA; verdadeiro resolve a regra <c>FADA_MULTIPLICATIVO</c> no
/// <c>regras_do_catalogo</c> (<see cref="IRegraCatalogoReader"/>) e a Base Legal
/// referenciada (<see cref="IBaseLegalDoFadaReader"/>, Configuração),
/// cujo snapshot é congelado em <see cref="ConfiguracaoDoFada"/>.
/// </summary>
/// <remarks>
/// Sem <c>[NonTransactional]</c>, de propósito: o <c>SELECT ... FOR UPDATE</c> de
/// <c>ObterParaMutacaoAsync</c> só serializa handlers concorrentes dentro da transação ambiente do
/// Wolverine (ADR-0004), e desligá-la destravaria o lock pessimista.
/// </remarks>
public static class DefinirFadaCommandHandler
{
    public static async Task<Result> Handle(
        DefinirFadaCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalDoFadaReader baseLegalDoFadaReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(baseLegalDoFadaReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirAsync(command, obtido.Value!, regraCatalogoReader, baseLegalDoFadaReader, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result> DefinirAsync(
        DefinirFadaCommand command,
        ProcessoSeletivo processo,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalDoFadaReader baseLegalDoFadaReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken) =>
        command.Aplica
            ? await AplicarAsync(command, processo, regraCatalogoReader, baseLegalDoFadaReader, unitOfWork, cancellationToken)
                .ConfigureAwait(false)
            : await NaoAplicarAsync(command, processo, unitOfWork, cancellationToken).ConfigureAwait(false);

    private static async Task<Result> NaoAplicarAsync(
        DefinirFadaCommand command,
        ProcessoSeletivo processo,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (command.TemConfiguracaoDoFada)
        {
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao,
                "O processo que não aplica o Fator de Atenuação das Desigualdades de Acesso (FADA) não pode ter a configuração do FADA."));
        }

        return await DefinirESalvarAsync(processo, aplica: false, fada: null, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AplicarAsync(
        DefinirFadaCommand command,
        ProcessoSeletivo processo,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalDoFadaReader baseLegalDoFadaReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ConfiguracaoDeclarada? declarada = ConfiguracaoDeclarada.De(command);
        if (declarada is null)
        {
            return Result.Failure(RecusaDaDeclaracaoIncompleta(command));
        }

        Result<ConfiguracaoDoFada> fada = await ResolverAsync(declarada, regraCatalogoReader, baseLegalDoFadaReader, cancellationToken)
            .ConfigureAwait(false);
        if (fada.IsFailure)
        {
            return Result.ValidationFailure(fada.Errors);
        }

        return await DefinirESalvarAsync(processo, aplica: true, fada.Value, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirESalvarAsync(
        ProcessoSeletivo processo, bool aplica, ConfiguracaoDoFada? fada, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result definido = processo.DefinirFada(aplica, fada);
        if (definido.IsFailure)
        {
            return Result.Failure(definido.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static async Task<Result<ConfiguracaoDoFada>> ResolverAsync(
        ConfiguracaoDeclarada declarada,
        IRegraCatalogoReader regraCatalogoReader,
        IBaseLegalDoFadaReader baseLegalDoFadaReader,
        CancellationToken cancellationToken)
    {
        Result<ReferenciaRegra> regra = await ResolverRegraAsync(declarada, regraCatalogoReader, cancellationToken).ConfigureAwait(false);
        if (regra.IsFailure)
        {
            return Result<ConfiguracaoDoFada>.Failure(regra.Error!);
        }

        Result<BaseLegalCongelada> baseLegal = await ResolverBaseLegalAsync(declarada.BaseLegalId, baseLegalDoFadaReader, cancellationToken)
            .ConfigureAwait(false);
        if (baseLegal.IsFailure)
        {
            return Result<ConfiguracaoDoFada>.Failure(baseLegal.Error!);
        }

        return ConfiguracaoDoFada.Criar(regra.Value!, declarada.Fator, declarada.Teto, declarada.FatoElegibilidadeCodigo, baseLegal.Value!);
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
                "ConfiguracaoDoFada.RegraNaoEncontrada",
                $"Regra do Fator de Atenuação das Desigualdades de Acesso (FADA) {declarada.RegraCodigo}/{declarada.RegraVersao} não encontrada no catálogo de regras."),
            { Tipo: not TipoRegra.RegraFada } => Falha<ReferenciaRegra>(
                "ConfiguracaoDoFada.RegraTipoInvalido",
                $"A regra {declarada.RegraCodigo}/{declarada.RegraVersao} não é do tipo `REGRA_FADA`."),
            _ => Result<ReferenciaRegra>.Success(regra.Referencia()),
        };
    }

    /// <summary>
    /// Reader cross-módulo (ADR-0056): o filtro global de soft-delete de Configuração já faz um Id inexistente e um
    /// desativado colapsarem no mesmo null — CA-02 não precisa distinguir os dois casos.
    /// </summary>
    private static async Task<Result<BaseLegalCongelada>> ResolverBaseLegalAsync(
        Guid baseLegalId, IBaseLegalDoFadaReader baseLegalDoFadaReader, CancellationToken cancellationToken)
    {
        BaseLegalDoFadaView? baseLegal = await baseLegalDoFadaReader
            .ObterPorIdAsync(baseLegalId, cancellationToken)
            .ConfigureAwait(false);

        return baseLegal is null
            ? Falha<BaseLegalCongelada>(
                "ConfiguracaoDoFada.BaseLegalNaoEncontrada",
                $"Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) {baseLegalId} não encontrada ou desativada.")
            : Result<BaseLegalCongelada>.Success(Congelar(baseLegal));
    }

    private static DomainError RecusaDaDeclaracaoIncompleta(DefinirFadaCommand command) =>
        command.Regra?.Codigo is null
            ? new(
                ProcessoSeletivoErrorCodes.FadaAplicaSemConfiguracao,
                "O processo que aplica o Fator de Atenuação das Desigualdades de Acesso (FADA) precisa da configuração do FADA.")
            : new(
                "ConfiguracaoDoFada.CamposObrigatorios",
                "A versão da regra, o fator e a base legal são obrigatórios quando o código da regra é informado.");

    private static Result<T> Falha<T>(string codigo, string mensagem) => Result<T>.Failure(new DomainError(codigo, mensagem));

    private static BaseLegalCongelada Congelar(BaseLegalDoFadaView baseLegal) =>
        new(
            baseLegal.Id,
            baseLegal.Codigo,
            baseLegal.TipoInstrumento,
            baseLegal.Identificacao,
            baseLegal.Descricao,
            [.. baseLegal.Municipios.Select(static m => (m.CodigoIbge, m.Nome, m.Uf))]);

    /// <summary>A configuração do FADA com todos os campos que o processo que o aplica precisa declarar.</summary>
    private sealed record ConfiguracaoDeclarada(
        string RegraCodigo,
        string RegraVersao,
        decimal Fator,
        decimal? Teto,
        string? FatoElegibilidadeCodigo,
        Guid BaseLegalId)
    {
        public static ConfiguracaoDeclarada? De(DefinirFadaCommand command) =>
            command is { Regra: { Codigo: { } codigo, Versao: { } versao }, Fator: { } fator, BaseLegal: { } baseLegal }
                ? new(codigo, versao, fator, command.Teto, command.FatoElegibilidadeCodigo, baseLegal.Id)
                : null;
    }
}
