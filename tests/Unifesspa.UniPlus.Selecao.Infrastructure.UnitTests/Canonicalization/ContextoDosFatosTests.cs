namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

public sealed class ContextoDosFatosTests
{
    [Fact]
    public void SerializarFatosDerivados_FatoPresente_EscreveRotulosDeclarados()
    {
        FatoDerivadoCongelado fato = new("DADO", "Dado", "Descrição", null, TipoDoDado.Texto,
            QuantidadeDeValores.ValorUnico, "INTERNO", "HIPOTESE", "Finalidade");

        JsonNode contexto = SerializacaoDosFatos.SerializarFatosDerivados(new Dictionary<string, FatoDerivadoCongelado> { [fato.Codigo] = fato })[0]!["contexto"]!;

        contexto["tipoRotulo"]!.GetValue<string>().Should().Be(fato.Dominio.Rotulo());
        contexto["quantidadeDeValoresRotulo"]!.GetValue<string>().Should().Be(fato.Cardinalidade.Rotulo());
    }

    [Fact]
    public void SerializarMetadadosFatos_MetadadoPresente_EscreveRotulosDeclarados()
    {
        MetadadoFatoCongelado metadado = new("DADO", TipoDoDado.Texto, OrigemFato.Declarado,
            QuantidadeDeValores.ValorUnico, "INSCRICAO", "DADO", null, null);

        JsonNode contexto = SerializacaoDosFatos.SerializarMetadadosFatos(new Dictionary<string, MetadadoFatoCongelado> { [metadado.Codigo] = metadado })[0]!["contexto"]!;

        contexto["tipoRotulo"]!.GetValue<string>().Should().Be(metadado.Dominio.Rotulo());
        contexto["origemRotulo"]!.GetValue<string>().Should().Be(metadado.Origem.Rotulo());
        contexto["quantidadeDeValoresRotulo"]!.GetValue<string>().Should().Be(metadado.Cardinalidade.Rotulo());
    }
}
