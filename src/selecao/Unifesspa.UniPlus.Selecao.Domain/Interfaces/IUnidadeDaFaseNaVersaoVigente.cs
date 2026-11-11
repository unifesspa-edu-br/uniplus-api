namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A fase de uma ação e a unidade que a conduz, lidas na versão publicada vigente do processo.
/// </summary>
public sealed record UnidadeDaFase(Guid FaseId, Guid UnidadeId);

/// <summary>
/// Lê a unidade responsável por uma fase na <b>versão publicada vigente</b> do processo, e nunca nas entidades vivas:
/// durante uma sessão de retificação elas guardam o rascunho, e a unidade que decide a ação da fase só muda quando a
/// retificação é fechada. Devolve <see langword="null"/> quando a fase não se resolve: processo ou fase inexistente,
/// fase de outro processo, etapa sem fase na versão, ou fase sem unidade.
/// </summary>
public interface IUnidadeDaFaseNaVersaoVigente
{
    /// <summary>A unidade da fase identificada, ou nulo quando a fase não se resolve no processo.</summary>
    Task<UnidadeDaFase?> DaFaseAsync(Guid processoId, Guid faseId, CancellationToken cancellationToken = default);

    /// <summary>A fase que contém a etapa e a unidade dela, ou nulo quando a etapa não se resolve numa fase do processo.</summary>
    Task<UnidadeDaFase?> DaEtapaAsync(Guid processoId, Guid etapaId, CancellationToken cancellationToken = default);
}
