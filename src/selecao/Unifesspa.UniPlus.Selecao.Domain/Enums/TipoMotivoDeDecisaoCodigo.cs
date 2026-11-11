namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoMotivoDeDecisao"/> e o código textual canônico
/// UPPER_SNAKE do wire — mesma convenção de <see cref="FundamentoIsencaoCodigo"/>, para que o
/// contrato não dependa da ordem de declaração no C#.
/// </summary>
public static class TipoMotivoDeDecisaoCodigo
{
    public const string IsencaoDaTaxa = "ISENCAO_DA_TAXA";
    public const string HomologacaoDasInscricoes = "HOMOLOGACAO_DAS_INSCRICOES";
    public const string AnaliseDocumental = "ANALISE_DOCUMENTAL";
    public const string Heteroidentificacao = "HETEROIDENTIFICACAO";
    public const string AvaliacaoBiopsicossocial = "AVALIACAO_BIOPSICOSSOCIAL";
    public const string Classificacao = "CLASSIFICACAO";

    private static readonly Dictionary<string, TipoMotivoDeDecisao> PorCodigo = new(StringComparer.Ordinal)
    {
        [IsencaoDaTaxa] = TipoMotivoDeDecisao.IsencaoDaTaxa,
        [HomologacaoDasInscricoes] = TipoMotivoDeDecisao.HomologacaoDasInscricoes,
        [AnaliseDocumental] = TipoMotivoDeDecisao.AnaliseDocumental,
        [Heteroidentificacao] = TipoMotivoDeDecisao.Heteroidentificacao,
        [AvaliacaoBiopsicossocial] = TipoMotivoDeDecisao.AvaliacaoBiopsicossocial,
        [Classificacao] = TipoMotivoDeDecisao.Classificacao,
    };

    /// <summary>Os códigos aceitos, na ordem de declaração do enum.</summary>
    public static IReadOnlyList<string> Codigos { get; } = [.. PorCodigo.Keys];

    /// <summary>Os códigos aceitos em texto corrido, para compor mensagens.</summary>
    public static string CodigosEmTexto { get; } = string.Join(", ", Codigos);

    public static string ToCodigo(this TipoMotivoDeDecisao tipo) =>
        PorCodigo.FirstOrDefault(par => par.Value == tipo).Key
        ?? throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoMotivoDeDecisao sem código canônico.");

    /// <summary>
    /// Converte o código do wire. Ausente, desconhecido ou fora da grafia canônica vira
    /// <see cref="TipoMotivoDeDecisao.Nenhum"/>, que quem chama trata como recusa.
    /// </summary>
    public static TipoMotivoDeDecisao FromCodigo(string? codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo, out TipoMotivoDeDecisao tipo)
            ? tipo
            : TipoMotivoDeDecisao.Nenhum;
}
