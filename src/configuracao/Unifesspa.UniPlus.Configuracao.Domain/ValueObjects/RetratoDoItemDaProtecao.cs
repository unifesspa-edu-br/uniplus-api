namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// O item do catálogo no instante de um ato da proteção de dados: a identidade, o código e o nome, a proteção
/// conferida e o estado do circuito (o ciclo e se havia manifestação desfavorável vigente). O ato guarda o retrato
/// porque o item pode mudar depois.
/// </summary>
/// <param name="Item">O cadastro e o identificador do item.</param>
/// <param name="Codigo">O código do item no instante do ato.</param>
/// <param name="Nome">O nome do item no instante do ato.</param>
/// <param name="Protecao">A proteção do item no instante do ato.</param>
/// <param name="Ciclo">O ciclo de aprovação em que o ato acontece.</param>
/// <param name="ManifestacaoDesfavoravelVigente">Se havia manifestação técnica desfavorável vigente no ciclo.</param>
public sealed record RetratoDoItemDaProtecao(
    ItemDaProtecao Item,
    string Codigo,
    string Nome,
    ProtecaoDeDados Protecao,
    CicloDaAprovacao Ciclo,
    bool ManifestacaoDesfavoravelVigente);
