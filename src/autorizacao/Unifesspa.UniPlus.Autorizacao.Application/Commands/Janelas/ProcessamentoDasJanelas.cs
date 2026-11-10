namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A rotina das janelas de migração: materializa o fim das que venceram ou concluíram e emite os lembretes de 48 e de 24
/// horas. A trilha não depende de a rotina ter rodado, porque a decisão de acesso confere o prazo e toda operação sobre
/// o perfil materializa o fim antes de decidir. Cada perfil vai num comando próprio, com a transação e a trava dele, e a
/// falha de um não impede os demais.
/// </summary>
public sealed partial class ProcessamentoDasJanelas
{
    private const string NomeDaTrava = "autorizacao.janelas_de_migracao";

    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly ICommandBus _bus;
    private readonly ITravaDaRotina _trava;
    private readonly ILogger<ProcessamentoDasJanelas> _logger;

    public ProcessamentoDasJanelas(IJanelaDeMigracaoRepository janelas, ICommandBus bus, ITravaDaRotina trava, ILogger<ProcessamentoDasJanelas> logger)
    {
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(trava);
        ArgumentNullException.ThrowIfNull(logger);

        _janelas = janelas;
        _bus = bus;
        _trava = trava;
        _logger = logger;
    }

    /// <summary>
    /// Processa as janelas não encerradas e devolve quantas tiveram o que fazer nesta execução; com outra instância na
    /// rotina, pula a execução.
    /// </summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        IAsyncDisposable? trava = await _trava.TentarObterAsync(NomeDaTrava, cancellationToken).ConfigureAwait(false);
        if (trava is null)
        {
            LogOutraInstanciaNaRotina(_logger);

            return 0;
        }

        await using (trava.ConfigureAwait(false))
        {
            return await ProcessarTodasAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> ProcessarTodasAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> perfis = await _janelas.ListarPerfisComJanelaNaoEncerradaAsync(cancellationToken).ConfigureAwait(false);
        int processadas = 0;

        foreach (Guid perfilId in perfis)
        {
            processadas += await ProcessarAsync(perfilId, cancellationToken).ConfigureAwait(false);
        }

        return processadas;
    }

    private async Task<int> ProcessarAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        try
        {
            Result<bool> resultado = await _bus.Send(new MaterializarJanelaVencidaCommand(perfilId), cancellationToken).ConfigureAwait(false);
            if (resultado.IsFailure)
            {
                LogFalhaAoProcessar(_logger, perfilId, null);
            }

            return resultado.IsSuccess && resultado.Value ? 1 : 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhaAoProcessar(_logger, perfilId, ex);

            return 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outra instância processa as janelas de migração; esta execução foi pulada")]
    private static partial void LogOutraInstanciaNaRotina(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível processar a janela de migração do perfil {PerfilId}")]
    private static partial void LogFalhaAoProcessar(ILogger logger, Guid perfilId, Exception? exception);
}
