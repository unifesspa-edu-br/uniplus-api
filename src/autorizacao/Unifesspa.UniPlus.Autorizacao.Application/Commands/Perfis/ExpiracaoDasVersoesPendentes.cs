namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Materializa o fim do prazo das versões pendentes. A trilha não depende de a rotina ter rodado, porque toda operação
/// sobre o perfil materializa o prazo vencido antes de decidir; a rotina garante que a versão que ninguém tocou também
/// termine expirada. Cada perfil é travado e conferido de novo, e a expiração que outra operação já fez não se repete.
/// </summary>
public sealed partial class ExpiracaoDasVersoesPendentes
{
    private readonly IPerfilRepository _perfis;
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpiracaoDasVersoesPendentes> _logger;

    public ExpiracaoDasVersoesPendentes(
        IPerfilRepository perfis, OperacaoSobreOPerfil operacao, TimeProvider clock, ILogger<ExpiracaoDasVersoesPendentes> logger)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _perfis = perfis;
        _operacao = operacao;
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

    private async Task<int> ExpirarAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        bool expirou = false;
        Result resultado = await _operacao
            .ExecutarAsync(perfilId, perfil => { expirou = perfil.MaterializarExpiracao(_clock.GetUtcNow()); return Result.Success(); }, cancellationToken)
            .ConfigureAwait(false);

        return Contar(perfilId, expirou, resultado);
    }

    private int Contar(Guid perfilId, bool expirou, Result resultado)
    {
        if (resultado.IsFailure)
        {
            LogFalhaAoExpirar(_logger, perfilId);
        }

        return expirou && resultado.IsSuccess ? 1 : 0;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível expirar a versão pendente do perfil {PerfilId}")]
    private static partial void LogFalhaAoExpirar(ILogger logger, Guid perfilId);
}
