namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma etapa de avaliação como o documento a traz. A posição é a da etapa na lista enviada, e a ordem dela é essa posição mais 1: o
/// documento não tem <c>ordem</c>. A chave é a que o autor escolheu e as outras partes citam. A janela própria ausente quer dizer
/// que a etapa segue a da fase.
/// </summary>
public sealed record EtapaNoDocumento(
    int PosicaoNoDocumento,
    string Chave,
    IdentificacaoDaEtapaNoDocumento Identificacao,
    AvaliacaoDaEtapaNoDocumento Avaliacao,
    JanelaDeTempo Janela,
    IReadOnlyList<BancaNoDocumento> Bancas)
{
    /// <summary>Os atos que a etapa publica, cada um com o papel; o campo ausente é a lista vazia.</summary>
    public IReadOnlyList<ProdutoNoDocumento> PublicacoesPrevistas { get; init; } = [];

    /// <summary>Se a etapa promete parecer individual a cada candidato; o campo ausente vale sem a promessa.</summary>
    public bool EmiteParecerIndividual { get; init; }

    /// <summary>Os tipos de decisão que a etapa declara produzir, como foram escritos; ausente é nulo.</summary>
    public IReadOnlyList<string?>? TiposDeDecisao { get; init; }

    /// <summary>Os prazos de recurso que a etapa abre, na ordem do documento; o campo ausente é a lista vazia.</summary>
    public IReadOnlyList<RecursoDaEtapaNoDocumento> Recursos { get; init; } = [];
}

/// <summary>
/// O que identifica a etapa e decide a nota dela, como o documento traz: o tipo de etapa pelo código do catálogo e a fase pela chave
/// que o documento declarou para ela. O que o documento não traz é nulo.
/// </summary>
public sealed record IdentificacaoDaEtapaNoDocumento(
    string? Nome,
    string? TipoEtapaCodigo,
    string? FaseChave,
    CaraterEtapa? Carater,
    decimal? Peso,
    decimal? NotaMinima);

/// <summary>Como a nota da etapa é lançada e de que itens e níveis ela se compõe, como o documento traz.</summary>
public sealed record AvaliacaoDaEtapaNoDocumento(
    ReferenciaDeRegraNoDocumento? RegraDeApuracao,
    FormaDeAvaliacao? Forma,
    decimal? NotaMaxima,
    decimal? PassoDoLancamento,
    IReadOnlyList<ItemDeAvaliacaoNoDocumento> Itens,
    IReadOnlyList<NivelDeAtendimentoNoDocumento> Niveis);

/// <summary>Um item de avaliação, com a posição dele na lista enviada e a chave que o documento declarou.</summary>
public sealed record ItemDeAvaliacaoNoDocumento(int PosicaoNoDocumento, string Chave, string? Rotulo, decimal? PontuacaoMaxima);

/// <summary>Um nível da escala de atendimento, com a posição dele na lista enviada e a chave que o documento declarou.</summary>
public sealed record NivelDeAtendimentoNoDocumento(int PosicaoNoDocumento, string Chave, string? Rotulo, decimal? Fracao);

/// <summary>
/// Um prazo de recurso da etapa, com a posição dele na lista enviada: de que evento o prazo corre e a regra, com o ato de
/// referência, os parâmetros e os procedimentos.
/// </summary>
public sealed record RecursoDaEtapaNoDocumento(
    int PosicaoNoDocumento,
    EventoInicialDoPrazo? EventoInicialDoPrazo,
    RegraDeRecursoNoDocumento Regra);
