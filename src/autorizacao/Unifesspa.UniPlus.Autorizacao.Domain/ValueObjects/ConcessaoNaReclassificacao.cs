namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Uma etapa já decidida para a concessão: quem decidiu e o que decidiu.</summary>
public sealed record DecisaoNaConcessao(EtapaDaVersao Etapa, Identidade Decisor, ResultadoDaLiberacao Resultado);

/// <summary>
/// A concessão como a decisão da reclassificação a enxerga: se ainda dá a permissão pela composição, a base dela, o escopo
/// em que foi concedida, o que as etapas já decidiram e se o decisor leu o documento de cada versão em uso.
/// </summary>
/// <param name="Id">A concessão.</param>
/// <param name="Beneficiario">Quem tem a concessão.</param>
/// <param name="DaPermissao">A concessão não revogada que reúne a permissão pela composição em vigor.</param>
/// <param name="Base">A base da permissão na concessão.</param>
/// <param name="EscopoTipo">O escopo em que a concessão foi dada.</param>
/// <param name="Decisoes">As etapas já decididas para a concessão nesta reclassificação.</param>
/// <param name="DocumentoLido">O decisor leu o documento instituidor da versão da concessão.</param>
public sealed record ConcessaoNaReclassificacao(
    Guid Id,
    Identidade Beneficiario,
    bool DaPermissao,
    BaseDaConcessao Base,
    TipoDeEscopo EscopoTipo,
    IReadOnlyList<DecisaoNaConcessao> Decisoes,
    bool DocumentoLido);
