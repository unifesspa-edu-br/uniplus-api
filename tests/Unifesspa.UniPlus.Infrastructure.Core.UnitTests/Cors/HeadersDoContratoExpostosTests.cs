namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Cors.Infrastructure;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class HeadersDoContratoExpostosTests
{
    private static readonly string[] HeadersComuns = ["ETag", "Idempotency-Replayed", "Link", "X-Page-Size", "X-Total-Count"];

    [Fact(DisplayName = "Header que um endpoint declara entra na lista exposta sem custar os comuns")]
    public void Politica_QuandoEndpointDeclaraHeader_DeveExpoLoAlemDosComuns()
    {
        CorsPolicy policy = PoliticaCorsDeTeste.Construir(allowAnyHeader: false, typeof(ControllerQueDeclaraLocation));

        policy.ExposedHeaders.Should().Contain("Location");
        policy.ExposedHeaders.Should().Contain(HeadersComuns);
    }

    [Fact(DisplayName = "Header declarado em mais de uma ação, ou já comum, aparece uma vez só, sem diferenciar caixa")]
    public void Politica_QuandoHeaderRepetidoOuJaComum_DeveListarUmaVez()
    {
        CorsPolicy policy = PoliticaCorsDeTeste.Construir(
            allowAnyHeader: false,
            typeof(ControllerQueDeclaraLocation),
            typeof(OutroControllerQueDeclaraLocation));

        policy.ExposedHeaders.Count(static h => string.Equals(h, "Location", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
        policy.ExposedHeaders.Count(static h => string.Equals(h, "ETag", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1, "o selo declarado por EmiteETag já consta dos comuns");
    }

    [Fact(DisplayName = "Sem controller que declare header, a política expõe só os comuns")]
    public void Politica_QuandoNenhumEndpointDeclaraHeader_DeveExporSoOsComuns()
    {
        CorsPolicy policy = PoliticaCorsDeTeste.Construir(allowAnyHeader: false, typeof(ControllerQueNaoDeclaraNada));

        policy.ExposedHeaders.Should().BeEquivalentTo(HeadersComuns);
    }

    [Fact(DisplayName = "Aplicação sem nenhum controller expõe só os comuns")]
    public void Politica_QuandoNaoHaControllers_DeveExporSoOsComuns()
    {
        CorsPolicy policy = PoliticaCorsDeTeste.Construir(allowAnyHeader: false);

        policy.ExposedHeaders.Should().BeEquivalentTo(HeadersComuns);
    }

    [Fact(DisplayName = "Header de um controller que não está na aplicação não é exposto")]
    public void Politica_QuandoHeaderEDeControllerAusente_NaoDeveExpoLo()
    {
        CorsPolicy policy = PoliticaCorsDeTeste.Construir(allowAnyHeader: false, typeof(ControllerQueNaoDeclaraNada));

        policy.ExposedHeaders.Should().NotContain("Location",
            "o header de um módulo só é exposto onde o módulo é servido");
    }

    [Fact(DisplayName = "Header de resposta declarado não altera os headers aceitos na requisição")]
    public void Politica_QuandoEndpointDeclaraHeader_NaoDeveAlterarOsHeadersAceitosNaRequisicao()
    {
        CorsPolicy comDeclaracao = PoliticaCorsDeTeste.Construir(allowAnyHeader: false, typeof(ControllerQueDeclaraLocation));
        CorsPolicy semDeclaracao = PoliticaCorsDeTeste.Construir(allowAnyHeader: false);

        comDeclaracao.Headers.Should().BeEquivalentTo(semDeclaracao.Headers);
    }

    [Fact(DisplayName = "Rota que declara a espera esgotada não expõe o Retry-After, que a resposta não traz")]
    public void Politica_QuandoRotaDeclaraEsperaEsgotada_NaoDeveExporORetryAfter()
    {
        CorsPolicy policy = PoliticaCorsDeTeste.Construir(allowAnyHeader: false, typeof(ControllerSujeitoATrava));

        policy.ExposedHeaders.Should().NotContain("Retry-After");
        policy.ExposedHeaders.Should().Contain(HeadersComuns);
    }

    [Fact(DisplayName = "Controller sem a declaração da espera esgotada não expõe o Retry-After")]
    public void Politica_QuandoNenhumaRotaDeclaraEsperaEsgotada_NaoDeveExporORetryAfter()
    {
        CorsPolicy policy = PoliticaCorsDeTeste.Construir(allowAnyHeader: false, typeof(ControllerQueNaoDeclaraNada));

        policy.ExposedHeaders.Should().NotContain("Retry-After");
    }

    private sealed class ControllerSujeitoATrava
    {
        [EmiteEsperaEsgotadaPelaTrava]
        public static void Alterar()
        {
        }
    }

    private sealed class ControllerQueDeclaraLocation
    {
        [EmiteHeader("Location", "Endereço do recurso criado.", Status = "201")]
        [EmiteETag]
        public static void Criar()
        {
        }
    }

    private sealed class OutroControllerQueDeclaraLocation
    {
        [EmiteHeader("location", "Endereço do recurso criado.", Status = "201")]
        public static void Criar()
        {
        }
    }

    private sealed class ControllerQueNaoDeclaraNada
    {
        public static void Listar()
        {
        }
    }
}
