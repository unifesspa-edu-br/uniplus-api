namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal sealed record CampoDeContexto(string Caminho, string Codigo, string Cadastro, IReadOnlyList<string> Atributos, string? CodigoComplementar = null);

/// <summary>Uma lista de códigos de um cadastro, com a lista de nomes só de leitura que a exportação põe ao lado dela, na mesma ordem.</summary>
internal sealed record ListaDeCodigosDeContexto(string Caminho, string Codigos, string Cadastro, string Nomes);

internal static class CamposDeContextoDoDocumento
{
    public const string Unidades = "unidades";
    public const string TiposDeProcesso = "tiposDeProcesso";
    public const string TiposDeDocumento = "tiposDeDocumento";
    public const string MotivosDeDecisao = "motivosDeDecisao";
    public const string TiposDeFase = "tiposDeFase";
    public const string TiposDeAto = "tiposDeAto";
    public const string CategoriasDeDocumento = "categoriasDeDocumento";
    public const string TiposDeBanca = "tiposDeBanca";
    public const string TiposDeEtapa = "tiposDeEtapa";
    public const string OfertasDeCurso = "ofertasDeCurso";
    public const string Modalidades = "modalidades";
    public const string CondicoesDeAtendimento = "condicoesDeAtendimento";
    public const string RecursosDeAcessibilidade = "recursosDeAcessibilidade";
    public const string TiposDeDeficiencia = "tiposDeDeficiencia";
    public const string BasesLegaisDaFada = "basesLegaisDaFada";

    public static IReadOnlyList<CampoDeContexto> Todos { get; } =
    [
        new("/identificacao", "tipoProcessoCodigo", TiposDeProcesso, ["tipoProcessoNome"]),
        new("/cronograma/fases/*", "unidadeResponsavelCodigo", Unidades, ["unidadeResponsavelNome", "unidadeResponsavelSigla"]),
        new("/documentosExigidos/exigencias/*", "tipoDocumentoCodigo", TiposDeDocumento,
            ["tipoDocumentoNome", "tipoDocumentoCategoria", "classificacaoProtecao", "finalidadeTratamento", "hipoteseLegal"]),
        new("/motivosDeDecisao/*", "codigo", MotivosDeDecisao,
            ["descricao", "subclassificacao", "sentido", "sentidoRotulo", "versaoDoEditalDeEntrada"], "tipoDeDecisaoCodigo"),
        new("/cronograma/fases/*", "tipoFaseCodigo", TiposDeFase, ["tipoFaseNome"]),
        new("/cronograma/fases/*/publicacoesPrevistas/*", "atoCodigo", TiposDeAto, ["atoNome"]),
        new("/etapas/*/publicacoesPrevistas/*", "atoCodigo", TiposDeAto, ["atoNome"]),
        new("/cronograma/fases/*/bancasRequeridas/*", "tipoBancaCodigo", TiposDeBanca, ["tipoBancaNome"]),
        new("/etapas/*/bancas/*", "tipoBancaCodigo", TiposDeBanca, ["tipoBancaNome"]),
        new("/etapas/*", "tipoEtapaCodigo", TiposDeEtapa, ["tipoEtapaNome"]),
        new("/distribuicaoVagas/*", "ofertaCursoCodigo", OfertasDeCurso, ["cursoNome", "cursoGrau", "unidadeOfertanteNome", "turnos"]),
        new("/distribuicaoVagas/*/modalidades/*", "codigo", Modalidades, ["descricao"]),
        new("/atendimento/condicoes/*", "condicaoCodigo", CondicoesDeAtendimento, ["condicaoNome"]),
        new("/atendimento/recursos/*", "recursoCodigo", RecursosDeAcessibilidade, ["recursoNome"]),
        new("/atendimento/tiposDeficiencia/*", "tipoDeficienciaCodigo", TiposDeDeficiencia, ["tipoDeficienciaNome"]),
        new("/fada", "baseLegalCodigo", BasesLegaisDaFada, ["tipoInstrumento", "identificacao", "descricao", "municipios"]),
    ];

    public static IReadOnlyList<ListaDeCodigosDeContexto> ListasDeCodigos { get; } =
    [
        new("/cronograma/fases/*/bancasRequeridas/*", "categoriasDocumentoCodigos", CategoriasDeDocumento, "categoriasDocumentoNomes"),
    ];

    public static bool Corresponde(string padrao, string caminho)
    {
        string[] partes = padrao.Split('/');
        string[] segmentos = caminho.Split('/');
        return partes.Length == segmentos.Length && partes.Zip(segmentos).All(static par =>
            par.First == par.Second || (par.First == "*" && int.TryParse(par.Second, out _)));
    }
}
