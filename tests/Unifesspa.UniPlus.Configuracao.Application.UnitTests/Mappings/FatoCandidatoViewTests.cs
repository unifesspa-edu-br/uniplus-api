namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.Cenarios;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class FatoCandidatoViewTests
{
    [Fact]
    public void Serializar_EixosDoFato_ExibeCodigoERotuloSemAcessores()
    {
        FatoCandidatoView fato = DadoDoCandidatoBuilder.Construir() with
        {
            Formato = CodigoComRotulo<FormatoTexto>.De(FormatoTexto.Livre),
            ComponenteExibicao = CodigoComRotulo<ComponenteDeExibicao>.De(ComponenteDeExibicao.Moeda),
        };
        using JsonDocument documento = JsonDocument.Parse(JsonSerializer.Serialize(fato, JsonSerializerOptions.Web));
        JsonElement raiz = documento.RootElement;

        Codigo(raiz, "formato").Should().Be("LIVRE");
        Codigo(raiz, "escopo").Should().Be("CANDIDATO");
        Codigo(raiz, "componenteExibicao").Should().Be("MOEDA");
        Codigo(raiz, "classificacaoProtecao").Should().Be("PESSOAL");
        Codigo(raiz, "hipoteseLegal").Should().Be("CONSENTIMENTO");
        ConferirAcessores(fato, raiz);
    }

    private static void ConferirAcessores(FatoCandidatoView fato, JsonElement raiz)
    {
        raiz.TryGetProperty("formatoValor", out _).Should().BeFalse();
        raiz.TryGetProperty("classificacaoProtecaoValor", out _).Should().BeFalse();
        fato.ClassificacaoProtecaoValor.Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    private static string? Codigo(JsonElement raiz, string campo)
    {
        JsonElement eixo = raiz.GetProperty(campo);
        eixo.GetProperty("rotulo").GetString().Should().NotBeNullOrWhiteSpace();
        return eixo.GetProperty("codigo").GetString();
    }
}
