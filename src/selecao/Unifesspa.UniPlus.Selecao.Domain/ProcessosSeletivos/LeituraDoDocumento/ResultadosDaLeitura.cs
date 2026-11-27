namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O valor que a leitura de um campo montou e as recusas que ela encontrou ao montá-lo.</summary>
internal sealed record ValorLido<T>(T Valor, IReadOnlyList<RecusaDaParte> Recusas);

/// <summary>
/// A recusa de uma parte da fase antes de a fase ter posição canônica: a parte pelo caminho relativo à fase (<c>produtos</c>,
/// <c>unidadeResponsavel</c>), o campo que a causa aponta, relativo à fase, a posição da parte no documento, a chave dela quando tem
/// e o erro. A parte do nível da lista (<see langword="null"/>) é a fase que a leitura não adotou.
/// </summary>
internal sealed record RecusaDaParte(string? Parte, string Campo, int PosicaoNoDocumento, string? Chave, DomainError Erro);

/// <summary>
/// A fase que a leitura adotou, ou <see langword="null"/> quando a recusou, com a posição dela no documento, as recusas das partes e
/// os itens das listas que ficaram nela, cada um com a posição em que veio.
/// </summary>
internal sealed record FaseLida(
    FaseCronograma? Fase,
    int PosicaoNoDocumento,
    string Chave,
    IReadOnlyList<RecusaDaParte> Recusas,
    ItensAdotadosDaFase Itens);

internal sealed record DeclaracaoDaFaseLida(
    DecisoesDeCronogramaDaFase Cronograma,
    PublicacoesDaFase Publicacoes,
    IReadOnlyList<BancaRequerida> Bancas,
    ItensAdotadosDaFase Itens,
    IReadOnlyList<RecusaDaParte> Recusas);

/// <summary>Os produtos, as bancas e os procedimentos de recurso que a leitura manteve na fase, cada um com a posição em que veio no documento.</summary>
internal sealed record ItensAdotadosDaFase(
    IReadOnlyList<(ProdutoDaFase Item, int PosicaoNoDocumento)> Produtos,
    IReadOnlyList<(BancaRequerida Item, int PosicaoNoDocumento)> Bancas,
    IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)> Procedimentos)
{
    internal static ItensAdotadosDaFase Nenhum { get; } = new([], [], []);
}

/// <summary>A regra de recurso que a leitura adotou, ou <see langword="null"/> quando recusou a regra inteira, e os procedimentos que ficaram nela.</summary>
internal sealed record RegraDeRecursoLida(
    RegraRecursoFase? Regra,
    IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)> Procedimentos)
{
    internal static RegraDeRecursoLida Nenhuma { get; } = new(null, []);
}

/// <summary>
/// As fases adotadas, na ordem canônica; o código e o id em memória da fase adotada por chave e as chaves das fases que a leitura
/// recusou, que as etapas e as exigências consultam para achar a fase que citam; e a origem que liga cada fase ao documento e guarda
/// as partes recusadas.
/// </summary>
internal sealed record FasesLidas(
    IReadOnlyList<FaseCronograma> Fases,
    IReadOnlyDictionary<string, string> CodigoPelaChave,
    IReadOnlyDictionary<string, Guid> IdPelaChave,
    IReadOnlySet<string> ChavesRecusadas,
    OrigemNoDocumento Origem);

/// <summary>A identidade declarada, a localidade e a convenção de contagem, que ficam no cronograma, e as partes recusadas da identificação.</summary>
internal sealed record IdentificacaoLida(
    IdentidadeDoProcessoDeclarada Identidade,
    LocalidadeRegente? Localidade,
    ReferenciaRegra? AlgoritmoDeContagem,
    IReadOnlyList<ParteRecusada> PartesRecusadas);
