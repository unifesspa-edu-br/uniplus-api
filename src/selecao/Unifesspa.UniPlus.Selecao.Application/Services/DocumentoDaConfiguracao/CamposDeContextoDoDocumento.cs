namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal sealed record CampoDeContexto(string Caminho, string Codigo, string Cadastro, IReadOnlyList<string> Atributos, string? CodigoComplementar = null);

internal static class CamposDeContextoDoDocumento
{
    public const string Unidades = "unidades";
    public const string TiposDeProcesso = "tiposDeProcesso";
    public const string TiposDeDocumento = "tiposDeDocumento";
    public const string MotivosDeDecisao = "motivosDeDecisao";

    public static IReadOnlyList<CampoDeContexto> Todos { get; } =
    [
        new("/identificacao", "tipoProcessoCodigo", TiposDeProcesso, ["tipoProcessoNome"]),
        new("/cronograma/fases/*", "unidadeResponsavelCodigo", Unidades, ["unidadeResponsavelNome", "unidadeResponsavelSigla"]),
        new("/documentosExigidos/exigencias/*", "tipoDocumentoCodigo", TiposDeDocumento,
            ["tipoDocumentoNome", "tipoDocumentoCategoria", "classificacaoProtecao", "finalidadeTratamento", "hipoteseLegal"]),
        new("/motivosDeDecisao/*", "codigo", MotivosDeDecisao,
            ["descricao", "subclassificacao", "sentido", "sentidoRotulo", "versaoDoEditalDeEntrada"], "tipoDeDecisaoCodigo"),
    ];

    public static bool Corresponde(string padrao, string caminho)
    {
        string[] partes = padrao.Split('/');
        string[] segmentos = caminho.Split('/');
        return partes.Length == segmentos.Length && partes.Zip(segmentos).All(static par =>
            par.First == par.Second || (par.First == "*" && int.TryParse(par.Second, out _)));
    }
}
