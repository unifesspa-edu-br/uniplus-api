namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// As mutações do bloco <c>divulgacao</c> que o decoder tem de recusar. Cada uma adultera UM aspecto do
/// bloco default (o corpus de teste não configura divulgação) e traz o código E o trecho da mensagem que
/// apontam o aspecto ofensivo — nunca apenas a falha, que passaria por qualquer outro motivo, nem apenas
/// o código, que um mesmo <c>EnvelopeMalformado</c> genérico compartilha entre guardas diferentes.
/// </summary>
public static class MutacoesDeDivulgacaoDoEnvelope
{
    public static IEnumerable<object[]> Todas() =>
        [.. SobreOsCamposPublicos(), .. SobreARegraDoNomeAbreviado(), .. SobreAFormaDoBloco(), .. SobreOFormatoDaJustificativa(),
         .. SobreOLimiteEOsCaracteresDaJustificativa()];

    private static IEnumerable<object[]> SobreOsCamposPublicos() =>
    [
        Caso("token estranho em camposPublicos (vocabulário fechado)", CamposComTokenEstranho,
            "ConfiguracaoDivulgacao.CampoNaoPermitido", "vocabulário de divulgação pública"),
        Caso("token repetido (sem repetição)", CamposComTokenRepetido,
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.camposPublicos"),
        Caso("array fora da ordem canônica (a ordenação faz parte dos bytes)", CamposForaDaOrdemCanonica,
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.camposPublicos"),
        Caso("sem numero_inscricao (o piso)", CamposSemNumeroDeInscricao,
            "ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio", "numero_inscricao"),
        Caso("nome e nome_abreviado juntos (formas excludentes)", NomeENomeAbreviadoJuntos,
            "ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes", "ao mesmo tempo"),
    ];

    private static IEnumerable<object[]> SobreARegraDoNomeAbreviado() =>
    [
        Caso("regraNomeAbreviado preenchido sem nome_abreviado (regra sem abreviação)",
            envelope => Bloco(envelope)["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente,
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.regraNomeAbreviado"),
        Caso("nome_abreviado sem regraNomeAbreviado (abreviação sem regra)",
            envelope => Bloco(envelope)["camposPublicos"] = Campos("nome_abreviado", "numero_inscricao"),
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.regraNomeAbreviado"),
        Caso("regra com identificador desconhecido (despacho por identificador conhecido)", RegraComIdentificadorDesconhecido,
            ErrosCodecEnvelope.EnvelopeMalformado, "regra_do_futuro"),
    ];

    private static IEnumerable<object[]> SobreAFormaDoBloco() =>
    [
        Caso("chave do bloco ausente (forma fechada)", envelope => Bloco(envelope).Remove("justificativa"),
            ErrosCodecEnvelope.EnvelopeMalformado, "'justificativa' está ausente"),
        Caso("chave intrusa no bloco (forma fechada)", envelope => Bloco(envelope)["chaveIntrusa"] = "x",
            ErrosCodecEnvelope.EnvelopeMalformado, "chaveIntrusa"),
        Caso("forma provisória antiga {\"status\":\"nao_construido\"} — o stub deixou de existir",
            envelope => envelope["divulgacao"] = new JsonObject { ["status"] = "nao_construido" },
            ErrosCodecEnvelope.EnvelopeMalformado, "'camposPublicos' está ausente"),
        Caso("tipo errado — camposPublicos como objeto, não array", envelope => Bloco(envelope)["camposPublicos"] = new JsonObject(),
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.camposPublicos"),
    ];

    /// <summary>
    /// Cada aspecto da justificativa é um caso independente. O caso de NFD real (combinantes de verdade)
    /// não está aqui: <c>PerfilCanonicoV1.Serializar</c>, que a adulteração usa para produzir os bytes
    /// finais, aplica NFC a toda string de negócio e "curaria" a própria adulteração antes de o decoder
    /// vê-la. Esse caso tem teste próprio, com um serializador que não recanonicaliza, e é pego pelo gate
    /// universal de forma canônica (<c>IntegridadeViolada</c>), não pelo guard do bloco.
    /// </summary>
    private static IEnumerable<object[]> SobreOFormatoDaJustificativa() =>
    [
        Caso("justificativa já em NFC com espaço nas bordas — só o trim falha",
            envelope => Bloco(envelope)["justificativa"] = "  Divulgação com espaço nas bordas.  ",
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.justificativa"),
        Caso("justificativa vazia — o codificador nunca emite essa forma, só null",
            envelope => Bloco(envelope)["justificativa"] = "",
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.justificativa"),
    ];

    private static IEnumerable<object[]> SobreOLimiteEOsCaracteresDaJustificativa() =>
    [
        Caso("justificativa acima do limite de 1000 caracteres da coluna",
            envelope => Bloco(envelope)["justificativa"] = new string('a', LimitesDoEnvelope.Justificativa + 1),
            ErrosCodecEnvelope.EnvelopeMalformado, "divulgacao.justificativa"),
        Caso("justificativa contém o caractere nulo (U+0000) — sobrevive a trim, NFC e ao limite de comprimento",
            envelope => Bloco(envelope)["justificativa"] = $"Divulgação com {(char)0} no meio.",
            "ConfiguracaoDivulgacao.JustificativaComCaractereNulo", "caractere nulo"),
    ];

    private static object[] Caso(string descricao, Action<JsonObject> mutar, string codigoEsperado, string trechoEsperado) =>
        [descricao, mutar, codigoEsperado, trechoEsperado];

    private static JsonObject Bloco(JsonObject envelope) => envelope["divulgacao"]!.AsObject();

    private static JsonArray Campos(params string[] campos) =>
        new JsonArray(campos.Select(campo => (JsonNode?)JsonValue.Create(campo)).ToArray());

    // errors[].message nunca ecoa o valor rejeitado: a mensagem é genérica, não "'cpf'".
    private static void CamposComTokenEstranho(JsonObject envelope) =>
        Bloco(envelope)["camposPublicos"] = Campos("cpf", "numero_inscricao");

    private static void CamposComTokenRepetido(JsonObject envelope) =>
        Bloco(envelope)["camposPublicos"] = Campos("numero_inscricao", "numero_inscricao");

    private static void CamposForaDaOrdemCanonica(JsonObject envelope) =>
        ComCamposEARegraVigente(envelope, "numero_inscricao", "nome_abreviado");

    private static void CamposSemNumeroDeInscricao(JsonObject envelope) =>
        ComCamposEARegraVigente(envelope, "nome_abreviado");

    private static void NomeENomeAbreviadoJuntos(JsonObject envelope)
    {
        ComCamposEARegraVigente(envelope, "nome", "nome_abreviado", "numero_inscricao");
        Bloco(envelope)["justificativa"] = "Justificativa qualquer.";
    }

    private static void RegraComIdentificadorDesconhecido(JsonObject envelope)
    {
        JsonObject bloco = Bloco(envelope);
        bloco["camposPublicos"] = Campos("nome_abreviado", "numero_inscricao");
        bloco["regraNomeAbreviado"] = "regra_do_futuro";
    }

    private static void ComCamposEARegraVigente(JsonObject envelope, params string[] campos)
    {
        JsonObject bloco = Bloco(envelope);
        bloco["camposPublicos"] = Campos(campos);
        bloco["regraNomeAbreviado"] = RegrasDeNomeAbreviado.Vigente;
    }
}
