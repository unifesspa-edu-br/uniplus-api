namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>Monta, para os testes, a forma que o esquema entregaria: valores, objetos com propriedades e listas.</summary>
internal static class FormaDeTeste
{
    public static FormaDoNo Valor(bool somenteLeitura = false, params string[] tiposDeclarados) =>
        new([], tiposDeclarados, somenteLeitura, parte: null, static () => new Dictionary<string, FormaDoNo>(), static () => null);

    public static FormaDoNo Objeto(Dictionary<string, FormaDoNo> propriedades, ParteComChave? parte = null) =>
        new([], ["object"], somenteLeitura: false, parte, () => propriedades, static () => null);

    public static FormaDoNo Lista(FormaDoNo itens, bool somenteLeitura = false) =>
        new([], ["array"], somenteLeitura, parte: null, static () => new Dictionary<string, FormaDoNo>(), () => itens);

    /// <summary>
    /// O documento de teste: raiz com <c>id</c> só de leitura; <c>etapas</c> com chave no documento, cada uma com <c>itens</c> de
    /// chave na etapa e <c>bancas</c> de chave no documento; <c>cronograma.fases</c> com chave no documento; e a identificação,
    /// com os descritivos que a exportação acrescenta ao lado dos códigos, só de leitura.
    /// </summary>
    public static FormaDoNo Documento() => Objeto(new()
    {
        ["id"] = Valor(somenteLeitura: true),
        ["nome"] = Valor(),
        ["etapas"] = Lista(Etapa()),
        ["cronograma"] = Objeto(new() { ["fases"] = Lista(Fase()) }),
        ["identificacao"] = Identificacao(),
        ["motivosDeDecisao"] = Lista(Motivo()),
        ["provaPresencial"] = Objeto(new() { ["municipios"] = Lista(Objeto(new() { ["codigoIbge"] = Valor(), ["nome"] = Valor(true), ["uf"] = Valor() })) }),
        ["documentosExigidos"] = Objeto(new() { ["exigencias"] = Lista(Exigencia()) }),
        ["cadastroDeReserva"] = Lista(ChamadaDoCadastroDeReserva()),
        ["classificacao"] = Classificacao(),
    });

    /// <summary>A classificação com a resolução de pesos citada e o quadro de pesos por área do ENEM, só de leitura.</summary>
    public static FormaDoNo Classificacao() => Objeto(new()
    {
        ["baseadoEmEnem"] = Valor(),
        ["resolucaoPesoAreaEnem"] = Valor(),
        ["quadroPesoAreaEnem"] = Lista(GrupoDoQuadroDePesos(), somenteLeitura: true),
    });

    private static FormaDoNo GrupoDoQuadroDePesos() => Objeto(new()
    {
        ["grupoAreaEnemCodigo"] = Valor(),
        ["grupoAreaEnemRotulo"] = Valor(),
        ["baseLegal"] = Valor(),
        ["areas"] = Lista(Objeto(new()
        {
            ["codigo"] = Valor(),
            ["rotulo"] = Valor(),
            ["peso"] = Valor(false, "number"),
            ["corte"] = Valor(false, "number"),
        })),
    });

    private static FormaDoNo ChamadaDoCadastroDeReserva() => Objeto(new()
    {
        ["numeroDaChamada"] = Valor(),
        ["pessoasDaReservaPorVaga"] = Valor(),
        ["participacaoDaReserva"] = Valor(),
        ["participacaoDaReservaRotulo"] = Valor(somenteLeitura: true),
    });

    private static FormaDoNo Motivo() => Objeto(new()
    {
        ["tipoDeDecisaoCodigo"] = Valor(),
        ["codigo"] = Valor(),
        ["descricao"] = Valor(true),
        ["subclassificacao"] = Valor(true),
        ["sentido"] = Valor(true),
        ["sentidoRotulo"] = Valor(true),
        ["versaoDoEditalDeEntrada"] = Valor(true),
    });

    private static FormaDoNo Exigencia() => Objeto(new()
    {
        ["chave"] = Valor(),
        ["tipoDocumentoCodigo"] = Valor(),
        ["tipoDocumentoNome"] = Valor(somenteLeitura: true),
        ["tipoDocumentoCategoria"] = Valor(somenteLeitura: true),
        ["classificacaoProtecao"] = Valor(somenteLeitura: true),
        ["finalidadeTratamento"] = Valor(somenteLeitura: true),
        ["hipoteseLegal"] = Valor(somenteLeitura: true),
        ["fatosComprovados"] = Valor(somenteLeitura: true),
    }, new ParteComChave("exigencia", EscopoDaChave.Documento));

    private static FormaDoNo Fase() => Objeto(new()
    {
        ["chave"] = Valor(),
        ["unidadeResponsavelCodigo"] = Valor(),
        ["unidadeResponsavelNome"] = Valor(somenteLeitura: true),
        ["unidadeResponsavelSigla"] = Valor(somenteLeitura: true),
    }, new ParteComChave("fase", EscopoDaChave.Documento));

    private static FormaDoNo Identificacao() => Objeto(new()
    {
        ["nome"] = Valor(),
        ["unidadeAdministradoraCodigo"] = Valor(),
        ["tipoProcessoCodigo"] = Valor(),
        ["unidadeNome"] = Valor(somenteLeitura: true),
        ["unidadeAdministradoraNome"] = Valor(somenteLeitura: true),
        ["unidadeAdministradoraSigla"] = Valor(somenteLeitura: true),
        ["tipoProcessoNome"] = Valor(somenteLeitura: true),
    });

    private static FormaDoNo Etapa() => Objeto(
        new()
        {
            ["chave"] = Valor(),
            ["nome"] = Valor(),
            ["descritivo"] = Valor(somenteLeitura: true),
            ["itens"] = Lista(Objeto(new() { ["chave"] = Valor(), ["rotulo"] = Valor() }, new ParteComChave("item", EscopoDaChave.ParteAncestral))),
            ["bancas"] = Lista(ParteComChaveNoDocumento("banca")),
        },
        new ParteComChave("etapa", EscopoDaChave.Documento));

    private static FormaDoNo ParteComChaveNoDocumento(string tipo) =>
        Objeto(new() { ["chave"] = Valor() }, new ParteComChave(tipo, EscopoDaChave.Documento));
}
