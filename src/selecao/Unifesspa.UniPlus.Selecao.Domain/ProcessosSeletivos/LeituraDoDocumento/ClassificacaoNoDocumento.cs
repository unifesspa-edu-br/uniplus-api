namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

/// <summary>
/// A classificação como o documento a traz: as regras do catálogo pelo código e pela versão, o arredondamento, a escala da nota
/// final e a resolução dos pesos por área do ENEM, que são nulos quando ausentes. O quadro de pesos por área que a exportação copia
/// é só de leitura e não entra.
/// </summary>
public sealed record ClassificacaoNoDocumento(
    ReferenciaDeRegraNoDocumento? RegraCalculo,
    ReferenciaDeRegraNoDocumento? RegraArredondamento,
    int? CasasArredondamento,
    decimal? EscalaNotaFinal,
    ReferenciaDeRegraNoDocumento? RegraOrdemAlocacao,
    bool? BaseadoEmEnem,
    string? ResolucaoPesoAreaEnem)
{
    /// <summary>As regras que eliminam candidatos, na ordem do documento; o campo ausente é a lista vazia.</summary>
    public IReadOnlyList<RegraDeEliminacaoNoDocumento> RegrasEliminacao { get; init; } = [];

    /// <summary>Os critérios de desempate; a ordem de cada um é a posição dele na lista, e o campo ausente é a lista vazia.</summary>
    public IReadOnlyList<CriterioDeDesempateNoDocumento> CriteriosDeDesempate { get; init; } = [];
}

/// <summary>
/// Uma regra de eliminação, com a posição dela na lista enviada. Cada regra traz só os parâmetros do seu código: a etapa pela chave
/// e a nota mínima, ou a área do ENEM e o mínimo.
/// </summary>
public sealed record RegraDeEliminacaoNoDocumento(
    int PosicaoNoDocumento,
    ReferenciaDeRegraNoDocumento Regra,
    string? EtapaChave,
    string? AreaCodigo,
    decimal? NotaMinima,
    decimal? Minimo);

/// <summary>Um critério de desempate, com a posição dele na lista enviada: o documento não tem <c>ordem</c>.</summary>
public sealed record CriterioDeDesempateNoDocumento(
    int PosicaoNoDocumento,
    ReferenciaDeRegraNoDocumento Regra,
    ParametrosDoDesempateNoDocumento Parametros);

/// <summary>Os parâmetros do critério de desempate como o documento os traz: a etapa pela chave, a idade mínima, as áreas do ENEM ou a condição sobre dado.</summary>
public sealed record ParametrosDoDesempateNoDocumento(string? EtapaChave, int? IdadeMinima, IReadOnlyList<string> Areas)
{
    /// <summary>O dado do candidato, o operador e o valor da condição, como o documento os traz; a leitura não confere nenhum dos três.</summary>
    public string? CodigoDoDado { get; init; }

    public string? Operador { get; init; }

    public JsonElement? Valor { get; init; }
}
