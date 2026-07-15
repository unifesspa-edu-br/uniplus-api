namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>FatoCandidato</c> para consumo cross-módulo via
/// <see cref="IFatoCandidatoReader"/> (ADR-0056, ADR-0111). Expõe o metadado do
/// fato — domínio, natureza, cardinalidade e o conjunto de valores — que o Módulo
/// Seleção lê ao validar um predicado de gatilho ou de desempate. Não carrega valor
/// de candidato algum.
/// </summary>
/// <param name="Id">Identificador único (Guid v7).</param>
/// <param name="Codigo">Código do fato, chave natural (ex.: "COR_RACA", "MODALIDADE").</param>
/// <param name="Nome">Rótulo legível do fato.</param>
/// <param name="Descricao">Descrição opcional (cosmética).</param>
/// <param name="Dominio">Tipo de dado — token canônico UPPER_SNAKE (CATEGORICO, BOOLEANO, NUMERICO).</param>
/// <param name="Natureza">Origem do dado — token canônico (BRUTO_INFORMADO, DE_VONTADE, DERIVADO).</param>
/// <param name="Cardinalidade">Cardinalidade — token canônico (ESCALAR, MULTIVALORADO).</param>
/// <param name="ValoresDominio">
/// Conjunto fechado de valores de um categórico estático, ou <see langword="null"/>
/// quando não há enumeração estática — booleano/numérico, ou categórico de
/// escopo-processo (cujos valores válidos vêm da oferta congelada do processo). O
/// <see langword="null"/> é significante e sobrevive ao round-trip (não é lista vazia).
/// </param>
public sealed record FatoCandidatoView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Dominio,
    string Natureza,
    string Cardinalidade,
    IReadOnlyList<string>? ValoresDominio);
