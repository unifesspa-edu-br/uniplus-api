namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json.Nodes;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

/// <summary>Descreve um documento com as descrições de cadastro que o teste escolhe, sobre a forma dos descritivos.</summary>
internal static class DocumentoDescritoDeTeste
{
    public static Task<JsonObject> DescreverAsync(string documento, params IDescricaoDeCadastro[] descricoes) =>
        DescreverAsync(documento, FormaDosDescritivosDeTeste.Documento(), descricoes);

    public static async Task<JsonObject> DescreverAsync(string documento, FormaDoNo forma, params IDescricaoDeCadastro[] descricoes)
    {
        IEsquemaDoDocumentoDaConfiguracao esquema = Substitute.For<IEsquemaDoDocumentoDaConfiguracao>();
        esquema.Forma.Returns(forma);
        JsonObject objeto = JsonNode.Parse(documento)!.AsObject();
        await new DescricaoDoDocumentoDoRascunho(esquema, descricoes).DescreverAsync(objeto, CancellationToken.None);
        return objeto;
    }

    public static IEnumerable<string> Chaves(JsonNode? objeto) => objeto!.AsObject().Select(static propriedade => propriedade.Key);

    public static IEnumerable<string> Textos(JsonNode? lista) => lista!.AsArray().Select(static item => item!.GetValue<string>());
}
