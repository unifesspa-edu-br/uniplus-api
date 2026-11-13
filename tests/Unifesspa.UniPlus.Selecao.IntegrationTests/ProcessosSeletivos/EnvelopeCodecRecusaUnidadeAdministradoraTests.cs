namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// A cidade da Unidade administradora congelada em <c>identidadesUnidade.administradora</c> (issue #1114): o
/// trio código IBGE, nome e UF é tudo-ou-nada e coerente com a referência de cidades.
/// </summary>
public sealed class EnvelopeCodecRecusaUnidadeAdministradoraTests
{
    [Fact(DisplayName = "issue #1114: cidade parcial (só código IBGE) em identidadesUnidade.administradora é recusada")]
    public void IdentidadesUnidade_CidadeParcialSoCodigo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["identidadesUnidade"]!["administradora"]!["cidadeNome"] = null);

        resultado.IsFailure.Should().BeTrue(
            "UnidadeAdministradoraSnapshot.Criar guarda o trio de cidade completo ou nenhum — o encoder nunca " +
            "emite código IBGE sem nome/UF");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("identidadesUnidade.administradora");
    }

    [Fact(DisplayName = "issue #1114: cidade parcial (só UF) em identidadesUnidade.administradora é recusada")]
    public void IdentidadesUnidade_CidadeParcialSoUf_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            envelope["identidadesUnidade"]!["administradora"]!["cidadeCodigoIbge"] = null;
            envelope["identidadesUnidade"]!["administradora"]!["cidadeNome"] = null;
        });

        resultado.IsFailure.Should().BeTrue(
            "trio parcial (só UF) é o mesmo tipo de bytes que não vêm do encoder — recusa como malformado");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "issue #1114: código IBGE em formato inválido em identidadesUnidade.administradora é recusado")]
    public void IdentidadesUnidade_CidadeCodigoIbgeFormatoInvalido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["identidadesUnidade"]!["administradora"]!["cidadeCodigoIbge"] = "ABCDEFG");

        resultado.IsFailure.Should().BeTrue(
            "o código IBGE tem de ter 7 dígitos numéricos — ReferenciaCidadeGeo.Validar recusa formato inválido");
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "issue #1114: UF incoerente com o prefixo do código IBGE em identidadesUnidade.administradora é recusada")]
    public void IdentidadesUnidade_CidadeUfIncoerente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["identidadesUnidade"]!["administradora"]!["cidadeUf"] = "SP");

        resultado.IsFailure.Should().BeTrue(
            "o prefixo '15' do código IBGE de Marabá corresponde a PA, incoerente com SP");
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }
}
