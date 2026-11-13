namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// O envelope canônico de referência, com o documento do edital e a exigência que oferece modelo
/// apontando os documentos semeados — a forma exata que a publicação congela.
/// </summary>
internal static class EnvelopeDeReferenciaComModelo
{
    public static byte[] Montar(
        ModeloDeDocumento modelo, string identificador, Guid editalId, string hashDoEdital, IReadOnlyList<AnexoDoEdital>? anexos = null)
    {
        JsonObject envelope = (JsonObject)JsonNode.Parse(EnvelopeCanonicoGoldenTests.CanonicalizarReferencia().Bytes)!;
        envelope["identificadorLegivel"] = identificador;
        envelope["identidadesUnidade"]!["administradora"]!["slug"] = AcervoPublicoDeTeste.UnidadeDoEnvelope;
        envelope["hashesEdital"] = HashesDoEdital(editalId, hashDoEdital, anexos ?? []);
        ApontarOModelo(envelope, modelo);

        return Encoding.UTF8.GetBytes(envelope.ToJsonString());
    }

    private static JsonObject HashesDoEdital(Guid editalId, string hashDoEdital, IReadOnlyList<AnexoDoEdital> anexos) => new()
    {
        ["documentoEditalId"] = editalId,
        ["hashSha256"] = hashDoEdital,
        ["anexos"] = new JsonArray([.. anexos.Select(static a => (JsonNode)new JsonObject
        {
            ["anexoId"] = a.Id,
            ["documentoId"] = a.DocumentoId,
            ["nomeArquivo"] = a.NomeArquivo,
            ["tipoDeMidia"] = a.TipoDeMidia,
            ["tamanhoBytes"] = a.TamanhoBytes,
            ["hashSha256"] = a.HashSha256,
        })]),
    };

    private static void ApontarOModelo(JsonObject envelope, ModeloDeDocumento modelo)
    {
        JsonObject[] comModelo = [.. envelope["documentosExigidos"]!["exigencias"]!.AsArray()
            .OfType<JsonObject>()
            .Where(static e => e["modelo"] is JsonObject)];
        comModelo.Should().NotBeEmpty("o envelope de referência precisa ter uma exigência com modelo para o teste ter objeto");
        foreach (JsonObject exigencia in comModelo)
        {
            exigencia["modelo"] = new JsonObject
            {
                ["modeloId"] = modelo.Id,
                ["nomeArquivo"] = modelo.NomeArquivo,
                ["formato"] = ModeloDeDocumento.TokenDe(modelo.Formato),
                ["hashSha256"] = modelo.HashSha256,
            };
        }
    }
}
