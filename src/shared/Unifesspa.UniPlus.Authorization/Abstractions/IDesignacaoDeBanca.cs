namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Porta pela qual a decisão pergunta quem está designado a uma banca. Quem guarda as designações a implementa; o ponto
/// de decisão só pergunta, sem conhecer o armazenamento. Nenhuma resposta é guardada entre chamadas: a designação e a
/// revogação valem na chamada seguinte.
/// </summary>
public interface IDesignacaoDeBanca
{
    /// <summary>
    /// Se a pessoa tem designação vigente à banca <b>e</b> essa banca é a da fase, da etapa e do processo informados: a
    /// designação a outra banca, ou a uma banca que não é do lugar pedido, não vale. A porta é quem conhece o lugar de
    /// cada banca, então o vínculo é conferido aqui, e nunca presumido pelo chamador.
    /// </summary>
    /// <param name="emissor">O emissor do token. O <c>sub</c> só identifica a pessoa dentro do emissor que o expediu.</param>
    /// <param name="subject">O <c>sub</c> do token.</param>
    /// <param name="processoId">O processo seletivo a que o ato pertence.</param>
    /// <param name="banca">A banca e o lugar dela: a fase e, quando o ato é de uma etapa, a etapa.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<bool> TemDesignacaoVigenteAsync(
        string emissor, string subject, Guid processoId, VinculosDaBanca banca, CancellationToken cancellationToken);

    /// <summary>Se alguém, quem quer que seja, está designado, e a designação vigente, à banca.</summary>
    /// <param name="bancaId">A banca.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<bool> ExisteDesignacaoVigenteAsync(Guid bancaId, CancellationToken cancellationToken);
}
