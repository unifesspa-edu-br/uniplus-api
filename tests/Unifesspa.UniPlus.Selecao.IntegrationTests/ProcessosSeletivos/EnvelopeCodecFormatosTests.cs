namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodecTipos;

public sealed class EnvelopeCodecFormatosTests
{
    [Theory(DisplayName = "Guid não-canônico (maiúsculo, com chaves, truncado) é recusado")]
    [InlineData("AAAA0000-0000-4000-8000-000000000001")]
    [InlineData("{aaaa0000-0000-4000-8000-000000000001}")]
    [InlineData("aaaa0000000040008000000000000001")]
    [InlineData("nao-e-um-guid")]
    public void GuidNaoCanonico_Recusa(string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["id"] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// O Guid vazio é sintaticamente um Guid — e é aí que ele engana. As factories reagem de
    /// dois modos, ambos errados: <c>EtapaProcesso.Reidratar</c> e os filhos do atendimento
    /// <b>lançam</b> (500 no meio de um descarte); já <c>ConfiguracaoDistribuicaoVagas.Criar</c>
    /// simplesmente o <b>aceita</b> como oferta de curso — e o grafo inválido restaura,
    /// persiste e faz round-trip perfeito.
    /// </summary>
    [Theory(DisplayName = "Guid VAZIO é recusado em qualquer campo de identidade")]
    [InlineData("etapas.0.id")]
    [InlineData("distribuicao.0.ofertaCursoOrigemId")]
    [InlineData("modalidades.0.modalidadeOrigemId")]
    [InlineData("atendimento.condicoes.0.condicaoOrigemId")]
    [InlineData("hashesEdital.documentoEditalId")]
    [InlineData("distribuicao.0.referenciaDemografica.origemId")]
    [InlineData("criteriosDesempate.1.parametros.etapaId")]
    public void GuidVazio_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            Definir(envelope, caminho, Guid.Empty.ToString()));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' com o Guid vazio ou explode numa factory (500 num descarte) ou entra como identidade " +
            "válida — nenhum dos dois é aceitável num documento com peso jurídico");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Guid VAZIO no bloco 'ofertas' é recusado")]
    public void GuidVazioEmOfertas_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["ofertas"]!.AsArray()[0] = Guid.Empty.ToString());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().BeOneOf(
            ErrosCodecEnvelope.EnvelopeMalformado,
            ErrosCodecEnvelope.BlocosDerivadosIncoerentes);
    }

    [Theory(DisplayName = "Enum fora do conjunto (ou com case trocado) é recusado")]
    [InlineData("carater", "classificatoria")]
    [InlineData("carater", "Inexistente")]
    [InlineData("carater", "Nenhum")]
    public void EnumInvalido_Recusa(string chave, string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]![chave] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Data fora do formato canônico é recusada")]
    public void DataInvalida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["periodo"]!["inicio"] = "02/03/2026");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// <c>0001-01-01</c> é como uma data <b>omitida</b> se materializa — e é por isso que os
    /// validators de publicar e de retificar a recusam explicitamente. <c>DadosEdital.Criar</c>
    /// só checa <c>fim &gt;= inicio</c>, e o default satisfaz isso; o encoder a reemite tal
    /// qual, então o round-trip passaria e o certame ficaria com um período de inscrição
    /// impossível.
    /// </summary>
    [Theory(DisplayName = "Data default (0001-01-01) é recusada — é como uma data omitida se materializa")]
    [InlineData("periodo.inicio")]
    [InlineData("periodo.fim")]
    public void DataDefault_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope => Definir(envelope, caminho, "0001-01-01"));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' zerado passa em DadosEdital.Criar (0001-01-01 <= 0001-01-01) e recanonicaliza nos mesmos " +
            "bytes — só os validators do caminho de escrita o barravam");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Número do ato além do limite dos validators (60) é recusado")]
    public void NumeroDoAtoAlemDoLimite_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["periodo"]!["numero"] = new string('9', 61));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Item de array que não é objeto é recusado")]
    public void ItemDeArrayNaoObjeto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0] = "sou uma string");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Campo obrigatório com null explícito é recusado")]
    public void ObrigatorioNulo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["nome"] = null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }
}
