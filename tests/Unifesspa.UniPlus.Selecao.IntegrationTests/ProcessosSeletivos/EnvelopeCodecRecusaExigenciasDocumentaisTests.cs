namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// As exigências documentais do envelope: o identificador de cada exigência é único, e o vínculo dela com a
/// fase e a etapa que a coletam, e com o formulário a que pertence, fecha contra o cronograma e as etapas
/// repostas ao lado.
/// </summary>
public sealed class EnvelopeCodecRecusaExigenciasDocumentaisTests
{
    // ── Story #923 — exigenciaId duplicado em documentosExigidos ──

    /// <summary>
    /// Um encoder real nunca produz <c>exigenciaId</c> duplicado — mas um envelope
    /// adulterado poderia. Sem <c>LeituraDoEnvelope.IndexarExigenciasPorId</c>, o
    /// <c>ToDictionary</c> ingênuo lançaria <see cref="ArgumentException"/> (500 não tratado
    /// no meio de uma restauração) em vez de recusar com um <see cref="DomainError"/> nomeado.
    /// </summary>
    [Fact(DisplayName = "Story #923: exigenciaId duplicado em documentosExigidos.exigencias é recusado, não lança")]
    public void ExigenciaIdDuplicado_Recusa()
    {
        VersaoConfiguracao versao = ProcessoComDuasExigenciasDeTeste.PublicarComEnvelopeAdulterado(DuplicarOIdDaPrimeiraExigencia);

        Result<EnvelopeReidratado> resultado = new RegistroCodecsEnvelope().Reidratar(versao);

        resultado.IsFailure.Should().BeTrue(
            "exigenciaId duplicado só é alcançável por adulteração — um encoder real nunca o produz — e a " +
            "restauração precisa recusar nomeadamente, não lançar ArgumentException do ToDictionary ingênuo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── O vínculo da exigência documental com a fase e a etapa que a coletam ──

    /// <summary>
    /// Onde o documento é coletado é dito por dois campos que só significam alguma coisa
    /// contra o cronograma e as etapas repostas ao lado deles.
    /// </summary>
    /// <remarks>
    /// A escrita confere os três vínculos antes de aceitar a exigência; a reidratação aceitava
    /// o que o documento dissesse. Uma exigência apontando para fase ou etapa que não existe
    /// no processo reposto atravessa a leitura e morre na chave estrangeira, como 500 no meio
    /// do descarte — e a que aponta para etapa de <b>outra</b> fase nem isso: persiste, e o
    /// certame restaurado passa a coletar o documento num dia que o cronograma não prevê.
    /// </remarks>
    [Fact(DisplayName = "Exigência apontando para fase fora do cronograma é recusada na reidratação")]
    public void ExigenciaComFaseForaDoCronograma_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            PrimeiraExigencia(envelope)["exigidoNaFaseId"] = Guid.CreateVersion7().ToString());

        resultado.IsFailure.Should().BeTrue(
            "a fase que coleta o documento tem de ser uma fase deste cronograma");
        resultado.Error!.Code.Should().Be("DocumentoExigido.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "Exigência apontando para etapa que o processo não tem é recusada na reidratação")]
    public void ExigenciaComEtapaForaDoProcesso_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            PrimeiraExigencia(envelope)["exigidoNaEtapaId"] = Guid.CreateVersion7().ToString());

        resultado.IsFailure.Should().BeTrue(
            "a etapa que coleta o documento tem de existir no processo reposto — senão a exigência restaurada " +
            "aponta para o nada, e quem descobre é a chave estrangeira");
        resultado.Error!.Code.Should().Be("DocumentoExigido.EtapaNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "Exigência apontando para etapa que não acontece na fase declarada é recusada na reidratação")]
    public void ExigenciaComEtapaDeOutraFase_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
        {
            // Uma etapa real do processo, mas que não declara a fase em que a exigência diz
            // ser coletada: é o caso que passa pela chave estrangeira e sobrevive calado.
            JsonObject etapa = envelope["etapas"]!.AsArray()[0]!.AsObject();
            etapa["faseCodigo"] = null;
            PrimeiraExigencia(envelope)["exigidoNaEtapaId"] = etapa["id"]!.GetValue<string>();
        });

        resultado.IsFailure.Should().BeTrue(
            "apontar etapa de outra fase diria que a habilitação coleta no dia da prova");
        resultado.Error!.Code.Should().Be("DocumentoExigido.EtapaNaoPertenceAFase");
    }

    /// <summary>
    /// O formulário a que o documento pertence é parte da forma da exigência: ausente ou fora do
    /// vocabulário, o bloco de comprovação não saberia em que formulário mostrá-lo.
    /// </summary>
    [Theory(DisplayName = "Exigência sem o formulário a que pertence, ou com formulário fora do vocabulário, é recusada na reidratação")]
    [InlineData(null)]
    [InlineData("MATRICULA")]
    public void ExigenciaComFinalidadeAusenteOuDesconhecida_Recusa(string? token)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            DefinirFinalidadeDaPrimeiraExigencia(envelope, token));

        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    private static void DuplicarOIdDaPrimeiraExigencia(JsonObject envelope)
    {
        JsonArray exigencias = envelope["documentosExigidos"]!["exigencias"]!.AsArray();
        exigencias.Should().HaveCount(2, "pré-condição: duas exigências para duplicar o id de uma na outra");
        exigencias[1]!["exigenciaId"] = exigencias[0]!["exigenciaId"]!.DeepClone();
    }

    private static void DefinirFinalidadeDaPrimeiraExigencia(JsonObject envelope, string? token)
    {
        if (token is null)
        {
            PrimeiraExigencia(envelope).Remove("finalidade");
        }
        else
        {
            PrimeiraExigencia(envelope)["finalidade"] = token;
        }
    }

    private static JsonObject PrimeiraExigencia(JsonObject envelope) =>
        envelope["documentosExigidos"]!["exigencias"]!.AsArray()[0]!.AsObject();
}
