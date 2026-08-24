namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Mapeamento único entre <see cref="FundamentoIsencao"/> e o código textual canônico
/// UPPER_SNAKE do wire de comando e do envelope (mesma convenção de
/// <see cref="TipoAbrangenciaCodigo"/>) — fonte de verdade única do wire format.
/// </summary>
public static class FundamentoIsencaoCodigo
{
    public const string CadastroUnico = "CADASTRO_UNICO";
    public const string DoacaoMedulaOssea = "DOACAO_MEDULA_OSSEA";

    /// <summary>
    /// Os fundamentos referenciáveis, na ordem de declaração do enum. A lista é derivada de
    /// <see cref="FundamentoIsencao"/>, não escrita à mão: um fundamento novo entra aqui ao
    /// ser declarado no enum, e a ausência do rótulo correspondente aparece como exceção na
    /// primeira leitura, em vez de virar um fundamento que a API deixa de anunciar.
    /// </summary>
    /// <remarks>
    /// <see cref="FundamentoIsencao.Nenhum"/> fica de fora porque é sentinela de ausência, e
    /// não um terceiro fundamento — é o que <see cref="FromCodigo"/> devolve para código
    /// desconhecido. Anunciá-lo daria a entender que existe algo chamado "nenhum" que um
    /// processo poderia referenciar.
    /// </remarks>
    public static IReadOnlyList<FundamentoIsencaoDescrito> Descritos { get; } =
    [
        .. Enum.GetValues<FundamentoIsencao>()
            .Where(static f => f != FundamentoIsencao.Nenhum)
            .Select(static f => new FundamentoIsencaoDescrito(f.ToCodigo(), NomeDe(f), DescricaoDe(f))),
    ];

    private static string NomeDe(FundamentoIsencao fundamento) => fundamento switch
    {
        FundamentoIsencao.CadastroUnico => "Cadastro Único",
        FundamentoIsencao.DoacaoMedulaOssea => "Doação de medula óssea",
        _ => throw new ArgumentOutOfRangeException(nameof(fundamento), fundamento, "FundamentoIsencao sem rótulo declarado."),
    };

    private static string DescricaoDe(FundamentoIsencao fundamento) => fundamento switch
    {
        FundamentoIsencao.CadastroUnico =>
            "Candidato de família de baixa renda inscrita no Cadastro Único do Governo Federal.",
        FundamentoIsencao.DoacaoMedulaOssea =>
            "Candidato doador de medula óssea.",
        _ => throw new ArgumentOutOfRangeException(nameof(fundamento), fundamento, "FundamentoIsencao sem descrição declarada."),
    };

    public static string ToCodigo(this FundamentoIsencao fundamento) => fundamento switch
    {
        FundamentoIsencao.CadastroUnico => CadastroUnico,
        FundamentoIsencao.DoacaoMedulaOssea => DoacaoMedulaOssea,
        FundamentoIsencao.Nenhum => throw new ArgumentOutOfRangeException(
            nameof(fundamento), fundamento, "FundamentoIsencao.Nenhum é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(fundamento), fundamento, "FundamentoIsencao desconhecido."),
    };

    public static FundamentoIsencao FromCodigo(string? codigo) => codigo switch
    {
        CadastroUnico => FundamentoIsencao.CadastroUnico,
        DoacaoMedulaOssea => FundamentoIsencao.DoacaoMedulaOssea,
        _ => FundamentoIsencao.Nenhum,
    };
}
