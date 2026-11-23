namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O envelope de referência do certame, com a única exigência dele, o cronograma e o fuso trocados pelo que o cenário
/// precisa; sem dizer nada, é o envelope de referência.
/// </summary>
internal sealed class EnvelopeDoCertameBuilder
{
    /// <summary>A fase de inscrição do envelope de referência.</summary>
    internal static readonly Guid FaseDaInscricao = Guid.Parse("00000000-0000-0000-0000-0000000003ee");

    private readonly JsonObject _envelope = ProjecaoDoCertameDeTeste.Envelope();

    public EnvelopeDoCertameBuilder ComPrazoDeEmissao(PrazoDeEmissao? prazo)
    {
        Exigencia["prazoDeEmissao"] = prazo is null ? null : new JsonObject
        {
            ["escolha"] = prazo.Escolha.Codigo(),
            ["valor"] = prazo.Valor,
            ["unidade"] = prazo.Unidade?.Codigo(),
            ["referencia"] = Referencia(prazo.Referencia),
            ["efeitoForaDoPrazo"] = prazo.EfeitoForaDoPrazo?.Codigo(),
        };
        return this;
    }

    public EnvelopeDoCertameBuilder ComValidade(ValidadeDoDocumento? validade)
    {
        Exigencia["validade"] = validade is null ? null : new JsonObject
        {
            ["escolha"] = validade.Escolha.Codigo(),
            ["referencia"] = Referencia(validade.Referencia),
            ["efeitoDoVencimento"] = validade.EfeitoDoVencimento?.Codigo(),
        };
        return this;
    }

    public EnvelopeDoCertameBuilder ComFases(params PeriodoDaFaseCongelada[] fases)
    {
        _envelope["cronogramaFases"]!["fases"] = new JsonArray(fases.Select(Fase).ToArray());
        return this;
    }

    public JsonObject Construir() => _envelope;

    private JsonObject Exigencia => _envelope["documentosExigidos"]!["exigencias"]![0]!.AsObject();

    private static JsonObject? Referencia(DataDeReferenciaDoDocumento? referencia) => referencia is null ? null : new JsonObject
    {
        ["tipo"] = referencia.Tipo.Codigo(),
        ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["faseId"] = referencia.FaseId?.ToString(),
    };

    private static JsonNode Fase(PeriodoDaFaseCongelada fase) => new JsonObject
    {
        ["id"] = fase.Id.ToString(),
        ["coletaInscricao"] = fase.ColetaInscricao,
        ["coletaSolicitacaoIsencao"] = false,
        ["permiteComplementacao"] = false,
        ["codigo"] = "INSCRICAO",
        ["ordem"] = 1,
        ["inicio"] = Instante(fase.Inicio),
        ["fim"] = Instante(fase.Fim),
    };

    private static string? Instante(DateTimeOffset? instante) =>
        instante?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
}
