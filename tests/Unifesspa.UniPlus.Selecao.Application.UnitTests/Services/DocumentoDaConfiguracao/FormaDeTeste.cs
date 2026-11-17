namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>Monta, para os testes, a forma que o esquema entregaria: valores, objetos com propriedades e listas.</summary>
internal static class FormaDeTeste
{
    public static FormaDoNo Valor(bool somenteLeitura = false) =>
        new(somenteLeitura, parte: null, static () => new Dictionary<string, FormaDoNo>(), static () => null);

    public static FormaDoNo Objeto(Dictionary<string, FormaDoNo> propriedades, ParteComChave? parte = null) =>
        new(somenteLeitura: false, parte, () => propriedades, static () => null);

    public static FormaDoNo Lista(FormaDoNo itens) =>
        new(somenteLeitura: false, parte: null, static () => new Dictionary<string, FormaDoNo>(), () => itens);

    /// <summary>
    /// O documento de teste: raiz com <c>id</c> só de leitura; <c>etapas</c> com chave no documento, cada uma com <c>itens</c> de
    /// chave na etapa e <c>bancas</c> de chave no documento; <c>cronograma.fases</c> com chave no documento; e a identificação.
    /// </summary>
    public static FormaDoNo Documento() => Objeto(new()
    {
        ["id"] = Valor(somenteLeitura: true),
        ["nome"] = Valor(),
        ["etapas"] = Lista(Etapa()),
        ["cronograma"] = Objeto(new() { ["fases"] = Lista(ParteComChaveNoDocumento("fase")) }),
        ["identificacao"] = Objeto(new() { ["nome"] = Valor(), ["unidadeAdministradoraCodigo"] = Valor(), ["unidadeNome"] = Valor(somenteLeitura: true) }),
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
