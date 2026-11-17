namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Materializa o fim do prazo das versões pendentes. A trilha não depende de a rotina ter rodado, porque toda operação
/// sobre o perfil materializa o prazo vencido antes de decidir; a rotina garante que a versão que ninguém tocou também
/// termine expirada. Cada perfil vai num comando próprio, com a transação e o acesso exclusivo a ele, e a falha de um não impede os
/// demais.
/// </summary>
public sealed partial class ExpiracaoDasVersoesPendentes
{
    private readonly IPerfilRepository _perfis;
    private readonly ICommandBus _bus;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpiracaoDasVersoesPendentes> _logger;

    public ExpiracaoDasVersoesPendentes(
        IPerfilRepository perfis, ICommandBus bus, TimeProvider clock, ILogger<ExpiracaoDasVersoesPendentes> logger)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _perfis = perfis;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Expira as pendentes vencidas e devolve quantas expirou nesta execução.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> vencidas = await _perfis.ListarComPendenteVencidaAsync(_clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        int expiradas = 0;

        foreach (Guid perfilId in vencidas)
        {
            expiradas += await ExpirarAsync(perfilId, cancellationToken).ConfigureAwait(false);
        }

        return expiradas;
    }

    private Task<int> ExpirarAsync(Guid perfilId, CancellationToken cancellationToken) =>
        ProcessamentoDoPerfil.ExecutarAsync(
            _bus, new ExpirarVersaoPendenteDoPerfilCommand(perfilId), excecao => LogFalhaAoExpirar(_logger, perfilId, excecao), cancellationToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível expirar a versão pendente do perfil {PerfilId}")]
    private static partial void LogFalhaAoExpirar(ILogger logger, Guid perfilId, Exception? exception);
}
