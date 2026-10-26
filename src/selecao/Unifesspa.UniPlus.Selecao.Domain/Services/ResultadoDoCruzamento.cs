namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Uma inscrição que casou com exatamente uma linha distinta do arquivo.</summary>
public sealed record InscritoComNota(
    Guid InscricaoId,
    string NumeroDeInscricaoNoEnem,
    IReadOnlyList<ResultadoDaAreaNoRetorno> Areas);

/// <summary>Uma inscrição com mais de uma linha diferente no arquivo, e quais são.</summary>
public sealed record ConflitoDeLinhas(Guid InscricaoId, IReadOnlyList<int> NumerosDasLinhas);

/// <summary>
/// O que o cruzamento do arquivo com as inscrições da edição produziu. Cada inscrição da edição
/// cai em exatamente um destes grupos, e cada linha do arquivo em no máximo um.
/// </summary>
public sealed record ResultadoDoCruzamento(
    IReadOnlyList<InscritoComNota> ComNota,
    IReadOnlyList<Guid> InscritosSemLinha,
    IReadOnlyList<int> LinhasSemInscrito,
    IReadOnlyList<ConflitoDeLinhas> Conflitos);
