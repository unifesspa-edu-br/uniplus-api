namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que a decisão da reclassificação precisa: a etapa, o resultado e o motivo; quem decide e se tem concessão do perfil; o
/// lote que agrupa as concessões decididas de uma vez; e as concessões alvo, já lidas sob a trava.
/// </summary>
/// <param name="Etapa">A etapa que o decisor exerce.</param>
/// <param name="Resultado">Libera ou mantém a suspensão.</param>
/// <param name="Motivo">O motivo; obrigatório ao manter a suspensão.</param>
/// <param name="Decisor">Quem decide.</param>
/// <param name="DecisorTemConcessao">O decisor tem concessão ativa do perfil.</param>
/// <param name="Todas">A decisão vale para todas as concessões que aguardam a etapa, e não para uma lista.</param>
/// <param name="LoteId">Agrupa as concessões decididas na mesma requisição.</param>
/// <param name="Instante">O instante da decisão, lido sob a trava.</param>
/// <param name="Vigente">A reclassificação é a mais recente do par (perfil, permissão) e o catálogo implantado ainda traz a permissão com a classe e o escopo que ela gravou.</param>
/// <param name="Alvos">As concessões alvo da decisão.</param>
public sealed record PedidoDeDecisaoDaReclassificacao(
    EtapaDaVersao Etapa,
    ResultadoDaLiberacao Resultado,
    string? Motivo,
    Identidade Decisor,
    bool DecisorTemConcessao,
    bool Todas,
    Guid LoteId,
    DateTimeOffset Instante,
    bool Vigente,
    IReadOnlyList<ConcessaoNaReclassificacao> Alvos);
