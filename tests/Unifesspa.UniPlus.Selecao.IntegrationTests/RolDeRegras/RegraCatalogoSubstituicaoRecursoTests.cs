namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Cobertura da substituição da regra de prazo de recurso (#854): a antiga
/// <c>RECURSO-MULTI-INSTANCIA</c> — que atribuía ao Uni+ a gestão de uma segunda
/// instância inexistente — dá lugar a <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c>, que
/// preserva a janela de suspensividade (por instância) e ancora o prazo num ato
/// publicado. Asserções sobre a fonte única do seed (<see cref="RegraCatalogoSeed.Itens"/>),
/// sem tocar o banco — a materialização é coberta por <see cref="RegraCatalogoSeedTests"/>.
/// </summary>
public sealed class RegraCatalogoSubstituicaoRecursoTests
{
    private const string CodigoAntigo = "RECURSO-MULTI-INSTANCIA";
    private const string CodigoNovo = "RECURSO-PRAZO-ANCORADO-EM-ATO";

    /// <summary>Hash da regra vigente: amarra a definição do seed ao hash gravado.</summary>
    private const string HashRegraNova =
        "42cbba27f7e36789437ecb469c74476c99a37baaf66404769774eeff619b9e2d";

    private static RegraCatalogoSeedItem RegraNova() =>
        RegraCatalogoSeed.Itens.Single(i => i.Codigo == CodigoNovo);

    [Fact(DisplayName = "CA-01 — a regra que gere a segunda instância não existe mais no catálogo")]
    public void RegraCatalogoSeed_RecursoMultiInstancia_NaoExiste()
    {
        RegraCatalogoSeed.Itens.Should().NotContain(
            i => i.Codigo == CodigoAntigo,
            "a substituição remove RECURSO-MULTI-INSTANCIA em toda versão");
    }

    [Fact(DisplayName = "CA-02 — a regra nova declara prazo ancorado em ato, sem a chave 'instancias'")]
    public void RegraCatalogoSeed_RecursoPrazoAncorado_EsquemaCorreto()
    {
        RegraCatalogoSeedItem nova = RegraNova();

        nova.Versao.Should().Be(RegraCatalogoSeed.VersaoV1);
        nova.Tipo.Should().Be(TipoRegra.RegraPrazoRecurso);

        using JsonDocument esquema = JsonDocument.Parse(nova.EsquemaArgsJson);
        JsonElement raiz = esquema.RootElement;

        raiz.ValueKind.Should().Be(JsonValueKind.Object);
        raiz.TryGetProperty("ato_ancora_codigo", out _).Should().BeFalse(
            "a âncora deixou de ser argumento declarado por código: ela é a identidade do produto " +
            "preliminar da própria fase, irmã de regra e args no envelope");
        raiz.TryGetProperty("suspensividade_primeira_instancia", out _).Should().BeTrue();
        raiz.TryGetProperty("suspensividade_segunda_instancia", out _).Should().BeTrue();
        raiz.TryGetProperty("instancias", out _).Should().BeFalse(
            "a lista de instâncias geridas pelo sistema foi descartada");

        // A âncora sai do esquema de argumentos, não do enunciado da regra: o que ela é e de
        // onde o prazo conta continua declarado nas invariantes.
        nova.InvariantesJson.Should().Contain("ancorado no instante de publicação do ato âncora");
        nova.InvariantesJson.Should().Contain("produto PRELIMINAR publicado pela própria fase");
    }

    [Fact(DisplayName = "CA-03 — nenhum campo da regra nova reintroduz a gestão da segunda instância")]
    public void RegraCatalogoSeed_RecursoPrazoAncorado_NaoGereSegundaInstancia()
    {
        RegraCatalogoSeedItem nova = RegraNova();

        // Substrings que identificavam, na regra antiga, a GESTÃO de uma segunda
        // instância dentro do Uni+ — a lista ordenada de instâncias, o órgão
        // externo que ela nomeava e a paralisação do processo. Nenhuma pode
        // ressurgir por acidente na definição da regra nova.
        string[] marcasDaGestao = ["instancias", "CONSEPE", "PARALISA"];
        string conteudo = string.Join(
            '\n',
            nova.EsquemaArgsJson,
            nova.InvariantesJson,
            nova.BaseLegal);

        foreach (string marca in marcasDaGestao)
        {
            conteudo.Should().NotContainEquivalentOf(
                marca,
                $"a correção não pode reintroduzir a gestão da segunda instância ('{marca}')");
        }
    }

    [Fact(DisplayName = "CA-06 — a janela de suspensividade sobrevive, por instância e anulável")]
    public void RegraCatalogoSeed_RecursoPrazoAncorado_PreservaSuspensividade()
    {
        RegraCatalogoSeedItem nova = RegraNova();

        using JsonDocument esquema = JsonDocument.Parse(nova.EsquemaArgsJson);
        JsonElement raiz = esquema.RootElement;

        string primeira = raiz.GetProperty("suspensividade_primeira_instancia").GetString()!;
        string segunda = raiz.GetProperty("suspensividade_segunda_instancia").GetString()!;

        // CA-06b: o nulo é a desativação — precisa estar declarado como válido nos
        // dois graus (é a configuração da Habilitação/Ingresso, via judicial).
        primeira.Should().Contain("null", "null = a pendência na fase não bloqueia atos irreversíveis");
        segunda.Should().Contain("null", "null = a pendência em instância superior não bloqueia (via judicial)");
    }

    [Fact(DisplayName = "CA-09 — o hash da regra nova é estável")]
    public void RegraCatalogoSeed_HashDaRegraNova()
    {
        RegraNova().ComputarHash().Should().Be(
            HashRegraNova,
            "a definição do seed deve bater com o hash gravado");
    }
}
