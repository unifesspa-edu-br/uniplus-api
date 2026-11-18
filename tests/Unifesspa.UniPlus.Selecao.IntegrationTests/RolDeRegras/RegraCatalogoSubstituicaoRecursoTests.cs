namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Cobertura da substituição da regra de prazo de recurso (#854): a antiga
/// <c>RECURSO-MULTI-INSTANCIA</c> — que atribuía ao Uni+ a gestão de uma segunda
/// instância inexistente — dá lugar a <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c>, que
/// preserva o período de suspensividade (por instância) e inicia o prazo em um ato
/// publicado. Asserções sobre a fonte única do seed (<see cref="RegraCatalogoSeed.Itens"/>),
/// sem tocar o banco — a materialização é coberta por <see cref="RegraCatalogoSeedTests"/>.
/// </summary>
public sealed class RegraCatalogoSubstituicaoRecursoTests
{
    private const string CodigoAntigo = "RECURSO-MULTI-INSTANCIA";
    private const string CodigoNovo = "RECURSO-PRAZO-ANCORADO-EM-ATO";

    /// <summary>Hash da regra vigente: amarra a definição do seed ao hash gravado.</summary>
    private const string HashRegraNova =
        "595a2d755e2f9ded25793c6f433964f36f4b9d314ef3a1183e45f57ed4d5bd02";

    private static RegraCatalogoSeedItem RegraNova() =>
        RegraCatalogoSeed.Itens.Single(i => i.Codigo == CodigoNovo);

    [Fact(DisplayName = "CA-01 — a regra que gere a segunda instância não existe mais no catálogo")]
    public void RegraCatalogoSeed_RecursoMultiInstancia_NaoExiste()
    {
        RegraCatalogoSeed.Itens.Should().NotContain(
            i => i.Codigo == CodigoAntigo,
            "a substituição remove RECURSO-MULTI-INSTANCIA em toda versão");
    }

    [Fact(DisplayName = "CA-02 — a regra nova declara prazo iniciado por ato, sem a chave 'instancias'")]
    public void RegraCatalogoSeed_RecursoPrazoAncorado_EsquemaCorreto()
    {
        RegraCatalogoSeedItem nova = RegraNova();

        nova.Versao.Should().Be(RegraCatalogoSeed.VersaoV1);
        nova.Tipo.Should().Be(TipoRegra.RegraPrazoRecurso);

        using JsonDocument esquema = JsonDocument.Parse(nova.DeclaracaoDaRegraJson);
        JsonElement raiz = esquema.RootElement;

        raiz.ValueKind.Should().Be(JsonValueKind.Object);
        raiz.EnumerateObject().Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra preserva as condições para o início do prazo")]
    public void RegraCatalogoSeed_RecursoPrazo_InvariantesPreservadas()
    {
        RegraCatalogoSeedItem nova = RegraNova();
        nova.InvariantesJson.Should().Contain("o prazo começa no instante da publicação do ato");
        nova.InvariantesJson.Should().Contain("publicação de um produto preliminar da própria fase");
    }

    [Theory(DisplayName = "Nenhum campo da regra reintroduz a gestão da segunda instância")]
    [InlineData("instancias")]
    [InlineData("CONSEPE")]
    [InlineData("PARALISA")]
    public void RegraCatalogoSeed_RecursoPrazo_NaoGereSegundaInstancia(string marca)
    {
        RegraCatalogoSeedItem nova = RegraNova();

        string conteudo = string.Join(
            '\n',
            nova.DeclaracaoDaRegraJson,
            nova.InvariantesJson,
            nova.BaseLegal);

        conteudo.Should().NotContainEquivalentOf(marca);
    }

    [Theory(DisplayName = "O documento admite a ausência dos parâmetros de suspensividade")]
    [InlineData("suspensividadePrimeiraInstanciaValor", "suspensividadePrimeiraInstanciaUnidade")]
    [InlineData("suspensividadeSegundaInstanciaValor", "suspensividadeSegundaInstanciaUnidade")]
    public void EsquemaDoDocumento_Suspensividade_DeclaraCamposOpcionais(string valor, string unidade)
    {
        JsonNode recurso = ArquivosDoEsquema.DoAssembly().PorNome["comuns.schema.json"]["$defs"]!["propriedadesDoRecurso"]!;
        recurso["properties"]![valor].Should().NotBeNull();
        recurso["properties"]![unidade].Should().NotBeNull();
        recurso["required"].Should().BeNull();
    }

    [Fact(DisplayName = "CA-09 — o hash da regra nova é estável")]
    public void RegraCatalogoSeed_HashDaRegraNova()
    {
        RegraNova().ComputarHash().Should().Be(
            HashRegraNova,
            "a definição do seed deve bater com o hash gravado");
    }
}
