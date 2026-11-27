namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.CorpoDoPedido;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.CorpoDoPedido.TextosNoLimite;

/// <summary>
/// O conjunto de documentos do modelo com um documento avulso por tipo de documento do catálogo semeado e uma base legal
/// de cada um. O conteúdo não tem teto de documentos nem de tamanho da base legal: o texto usa o tamanho das bases legais do catálogo.
/// </summary>
internal static class DocumentosNoLimite
{
    private const int TamanhoDaBaseLegal = 500;
    private const int TamanhoDoCodigo = 60;

    public static ConjuntoDeDocumentosDoModeloInput Conjunto(DimensoesDasSementesDoModelo dimensoes, int quantidade) => new(
        [.. Enumerable.Range(0, quantidade).Select(i => Documento(dimensoes, i))],
        []);

    private static DocumentoExigidoDoModeloInput Documento(DimensoesDasSementesDoModelo dimensoes, int indice) => new(
        Codigo(TamanhoDoCodigo, indice),
        Codigo(TamanhoDoCodigo, indice),
        Predicado(dimensoes, TamanhoDoCodigo),
        new IndeferimentoDoDocumentoDoModeloInput(true, MaiorCodigo<ConsequenciaDoDescumprimento>(), [BaseLegal()], true),
        Requisitos(),
        new ExigenciaPorOpcaoInput(Codigo(TamanhoDoCodigo, indice), [.. Enumerable.Range(0, dimensoes.ValoresPorOpcao).Select(i => Codigo(TamanhoDoCodigo, i))]));

    private static BaseLegalDoModeloInput BaseLegal() => new(
        Texto(TamanhoDaBaseLegal), MaiorCodigo<TipoAbrangencia>(), MaiorCodigo<StatusBaseLegal>(), Texto(TamanhoDaBaseLegal));

    private static RequisitosDoDocumentoDoModeloInput Requisitos() => new(
        new PrazoDeEmissaoDoModeloInput(MaiorCodigo<EscolhaDoPrazoDeEmissao>(), 365, MaiorCodigo<UnidadeDoPrazoDeEmissao>(), null, MaiorCodigo<EfeitoNaAnaliseDoDocumento>()),
        new ValidadeDoDocumentoDoModeloInput(MaiorCodigo<EscolhaDaValidade>(), null, MaiorCodigo<EfeitoNaAnaliseDoDocumento>()));
}
