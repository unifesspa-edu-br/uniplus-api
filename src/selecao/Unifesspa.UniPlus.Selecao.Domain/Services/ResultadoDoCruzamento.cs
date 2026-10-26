namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Uma inscrição que casou com exatamente uma linha distinta do arquivo.</summary>
public sealed record InscritoComNota(
    Guid InscricaoId,
    string NumeroDeInscricaoNoEnem,
    IReadOnlyList<ResultadoDaAreaNoRetorno> Areas);

/// <summary>Uma inscrição com mais de uma linha diferente no arquivo, e quais são.</summary>
public sealed record ConflitoDeLinhas(Guid InscricaoId, IReadOnlyList<int> NumerosDasLinhas);

/// <summary>Uma linha do arquivo cujo CPF não é de nenhuma inscrição: o número da linha, o CPF já mascarado, o nome e o sexo que o arquivo traz.</summary>
public sealed record LinhaSemInscritoNoCruzamento(int NumeroDaLinha, string CpfMascarado, string? Nome, string? Sexo);

/// <summary>
/// O que o cruzamento do arquivo com as inscrições da edição produziu. Cada inscrição da edição
/// cai em exatamente um destes grupos, e cada linha do arquivo em no máximo um.
/// </summary>
public sealed record ResultadoDoCruzamento(
    IReadOnlyList<InscritoComNota> ComNota,
    IReadOnlyList<Guid> InscritosSemLinha,
    IReadOnlyList<LinhaSemInscritoNoCruzamento> LinhasSemInscrito,
    IReadOnlyList<ConflitoDeLinhas> Conflitos);
