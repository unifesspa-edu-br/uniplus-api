namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As etapas que a leitura adotou, na ordem canônica, e o que as partes seguintes (a classificação) precisam delas: o id em memória
/// de cada etapa pela chave que o documento declarou, as chaves das etapas que a leitura recusou e as recusas e as origens que ligam
/// cada parte adotada à posição em que veio. A chave recusada se distingue da inexistente: quem cita a recusada não acumula causa nova.
/// </summary>
internal sealed record EtapasLidas(
    IReadOnlyList<EtapaProcesso> Etapas,
    IReadOnlyDictionary<string, Guid> IdPelaChave,
    IReadOnlySet<string> ChavesRecusadas,
    IReadOnlyList<RecusaDaParte> Recusas,
    IReadOnlyList<OrigemNoDocumento> Origens);

/// <summary>
/// A etapa que a leitura adotou, ou <see langword="null"/> quando a recusou, com a posição dela no documento, a chave, as recusas das
/// partes e os itens das listas que ficaram nela, cada um com a posição em que veio.
/// </summary>
internal sealed record EtapaLida(
    EtapaProcesso? Etapa,
    int PosicaoNoDocumento,
    string Chave,
    IReadOnlyList<RecusaDaParte> Recusas,
    ItensAdotadosDaEtapa Itens);

/// <summary>Os produtos, as bancas e os recursos que a leitura manteve na etapa, cada um com a posição em que veio no documento.</summary>
internal sealed record ItensAdotadosDaEtapa(
    IReadOnlyList<(ProdutoDaEtapa Item, int PosicaoNoDocumento)> Produtos,
    IReadOnlyList<(BancaDaEtapa Item, int PosicaoNoDocumento)> Bancas,
    IReadOnlyList<RecursoLido> Recursos)
{
    internal static ItensAdotadosDaEtapa Nenhum { get; } = new([], [], []);
}

/// <summary>O recurso da etapa que a leitura adotou, com a posição em que veio e os procedimentos que ficaram nele.</summary>
internal sealed record RecursoLido(
    RecursoDaEtapa Recurso,
    int PosicaoNoDocumento,
    IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)> Procedimentos);

/// <summary>O que a leitura da declaração de uma etapa montou, antes de saber se o tipo dela existe, e as recusas que acumulou.</summary>
internal sealed record DeclaracaoDaEtapaLida(
    string? FaseCodigo,
    AvaliacaoLidaDaEtapa Avaliacao,
    PublicacoesDaEtapa Publicacoes,
    IReadOnlyList<BancaDaEtapa> Bancas,
    ItensAdotadosDaEtapa Itens,
    IReadOnlyList<RecusaDaParte> Recusas);

/// <summary>
/// A avaliação da etapa como a leitura a montou: a declaração da nota, os itens com a chave que o documento declarou, o
/// identificador de cada item pela chave e as recusas.
/// </summary>
internal sealed record AvaliacaoLidaDaEtapa(
    DeclaracaoDaAvaliacaoDaEtapa Declaracao,
    IReadOnlyList<ItemDeAvaliacao> Itens,
    IReadOnlyDictionary<string, Guid> ItensPelaChave,
    IReadOnlyList<RecusaDaParte> Recusas);
