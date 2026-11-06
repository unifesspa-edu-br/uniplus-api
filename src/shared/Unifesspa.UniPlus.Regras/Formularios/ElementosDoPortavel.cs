namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>Uma etapa: o código com que o avaliador a identifica, a exibição da etapa inteira, os itens e os grupos.</summary>
public sealed record EtapaPortavel(
    string Codigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    IReadOnlyList<ItemPortavel> Itens,
    IReadOnlyList<GrupoPortavel> Grupos);

/// <summary>
/// Um campo: o fato que ele produz, a exibição, a obrigatoriedade — <c>SEMPRE</c>, <c>NUNCA</c> ou
/// <c>QUANDO</c> com o predicado —, as restrições de valor, o impedimento, a oferta de valores, quando o
/// campo tem uma — a resposta fora dela não vale —, e o formato do campo de texto, como <c>CPF</c> ou
/// <c>EMAIL</c>, que a resposta tem de atender.
/// </summary>
public sealed record ItemPortavel(
    string FatoCodigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade,
    IReadOnlyList<RestricaoValorInput> Restricoes,
    ImpedimentoInput? Impedimento,
    IReadOnlyList<string>? Oferta,
    string? Formato = null);

/// <summary>Um grupo repetível: exibição, obrigatoriedade, mínimo e máximo de ocorrências, se o candidato é um dos membros e os subitens.</summary>
public sealed record GrupoPortavel(
    string Codigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade,
    int Minimo,
    int? Maximo,
    bool IncluiCandidato,
    IReadOnlyList<ItemPortavel> Subitens);

/// <summary>Um termo exigido: o código com que o avaliador o identifica, a exibição e a obrigatoriedade.</summary>
public sealed record TermoPortavel(
    string Codigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade);

/// <summary>
/// As regras de um fato derivado: booleano (alguma regra ativa) ou categórico (a união do que as regras
/// ativas contribuem).
/// </summary>
public sealed record DerivacaoPortavel(string FatoCodigo, bool Booleano, IReadOnlyList<RegraDerivacaoPortavel> Regras);

/// <summary>Uma regra de derivação: quando ela ativa — a lista vazia é a âncora, sempre ativa — e o código que contribui no categórico.</summary>
public sealed record RegraDerivacaoPortavel(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando, string? Contribui);

/// <summary>Um fato agregado sobre um grupo: o fato de membro que ele agrega e a operação, <c>EXISTE</c> ou <c>VALORES_PRESENTES</c>.</summary>
public sealed record AgregadoPortavel(string Codigo, string GrupoCodigo, string FatoDeMembro, string Operacao);
