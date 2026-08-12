namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="FundamentoIsencao"/> e o código textual canônico
/// UPPER_SNAKE do wire de comando e do envelope (mesma convenção de
/// <see cref="TipoAbrangenciaCodigo"/>) — fonte de verdade única do wire format.
/// </summary>
public static class FundamentoIsencaoCodigo
{
    public const string CadastroUnico = "CADASTRO_UNICO";
    public const string DoacaoMedulaOssea = "DOACAO_MEDULA_OSSEA";

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
