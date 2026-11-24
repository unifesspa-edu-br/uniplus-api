namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Cobertura da substituição da regra de prazo de recurso: a antiga
/// <c>RECURSO-MULTI-INSTANCIA</c> — que atribuía ao Uni+ a gestão de uma segunda
/// instância inexistente — dá lugar a <c>RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO</c>, que
/// declara o efeito suspensivo, a guarda da vaga e o prazo da 2ª instância e inicia o prazo em um ato
/// publicado. Asserções sobre a fonte única do seed (<see cref="RegraCatalogoSeed.Itens"/>),
/// sem tocar o banco — a materialização é coberta por <see cref="RegraCatalogoSeedTests"/>.
/// </summary>
public sealed class RegraCatalogoSubstituicaoRecursoTests
{
    private const string CodigoAntigo = "RECURSO-MULTI-INSTANCIA";
    private const string CodigoNovo = "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO";

    /// <summary>Hash da regra vigente: amarra a definição do seed ao hash gravado.</summary>
    private const string HashRegraNova =
        "cffb7c530a7664cfd44d4750daa2974a1800cf0b73ff084039f701e2b453b2dc";

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
        nova.InvariantesJson.Should().Contain("começa no instante da publicação do ato");
        nova.InvariantesJson.Should().Contain("publicação de uma publicação prevista preliminar da própria fase");
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

    [Theory(DisplayName = "O documento declara os parâmetros do recurso como opcionais na forma: o domínio confere cada um")]
    [InlineData("prazoSegundaInstanciaValor", "prazoSegundaInstanciaUnidade")]
    [InlineData("efeitoSuspensivo", "guardaDaVaga")]
    public void EsquemaDoDocumento_ParametrosDoRecurso_DeclaraCamposOpcionais(string primeiro, string segundo)
    {
        JsonNode recurso = ArquivosDoEsquema.DoAssembly().PorNome["comuns.schema.json"]["$defs"]!["propriedadesDoRecurso"]!;
        recurso["properties"]![primeiro].Should().NotBeNull();
        recurso["properties"]![segundo].Should().NotBeNull();
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
