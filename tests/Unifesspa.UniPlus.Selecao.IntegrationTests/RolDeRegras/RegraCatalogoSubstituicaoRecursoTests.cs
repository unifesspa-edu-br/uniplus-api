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

    /// <summary>
    /// Hash da regra 18 no seed original (migration <c>AddRolDeRegras</c>), antes
    /// da substituição — a contraprova de que o hash da regra nova mudou.
    /// </summary>
    private const string HashRegraRemovida =
        "660cf3fffe22069f5a7f302a98b1e44b96d2e992680f02304935a36548f95490";

    /// <summary>
    /// Hash da regra nova, congelado na migration <c>SubstituiRegraRecursoMultiInstancia</c>.
    /// Amarra a definição do seed ao literal da migration: editar o texto da regra
    /// sem regenerar a migration quebra este teste.
    /// </summary>
    private const string HashRegraNova =
        "94f2a02a12cccae0ebe98dabc9dc66b5aacac25053e91b768fdf0d47492e8240";

    /// <summary>
    /// Hashes das 17 regras não tocadas, como gravados na migration original — a
    /// referência que prova que a substituição alterou UMA linha e só uma (CA-04).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> HashesOriginaisDasDemais =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FORMULA-MEDIA-PONDERADA"] = "a6fdc29bce533ef3cf8acb6cfdd7d67a99104541f6201d227f0d1c7051229ad6",
            ["CLASSIFICACAO-IMPORTADA"] = "8e6f1f7b705c601991bf0617bbf881e39a3666c641abdfa4c1bd9ce2ecbcc85c",
            ["PRECISAO-TRUNCAR"] = "66cbb932e61982f09fdd1d60a0db8411ea02dedc19bb72d1af23004f1e18360b",
            ["PRECISAO-ARREDONDAR-CIMA"] = "efc491bf6a242a870c7bf9969048c198db88116ba39e72a44bcf1d5d5aebc4fb",
            ["ELIM-NOTA-MINIMA-ETAPA"] = "b64f643eba9744efc20bf19221bde85c645da32262a7d2ef616f18bd7c2ed5ac",
            ["ELIM-CORTE-REDACAO"] = "6a23db02c00878d5bb98a445e8dc72e209a95f2aa4a8bfd91de8fa08ee69c240",
            ["ELIM-ZERO-EM-AREA"] = "75f4929b848138c8ed939a182e3664451ce38cd40a136adda438b62e1b6e3fb8",
            ["BONUS-MULTIPLICATIVO"] = "d824fa64884e038e132f918dfd6efecf56d1df4cd4a1a1f86a78bf31718dae9b",
            ["DESEMPATE-IDOSO"] = "6738b1a9ca4f063f7f215cabb837e055d8abafaf0835f0b3deeed1c97f0becd4",
            ["DESEMPATE-MAIOR-NOTA-ETAPA"] = "1a988e6681b2970dc6c568c7372ceaf2f6e5ec0f7061ac47f656e1366a9ecad8",
            ["DESEMPATE-MAIOR-IDADE"] = "1efa26eaeffc88baf31ce9e2030c05c9976fae125e9845fe0283168050dc1237",
            ["DESEMPATE-PREDICADO-FATO"] = "d832d910826f25b6b50fd324f2f3cae472440c0e81e082be7c8d4fefe3de3f21",
            ["ALOCACAO-OPCOES-RN04"] = "2bb69f0e34483e635aa0903f8d3ba19a4255e8f542c5f7090ac75cecf200c988",
            ["RENDA-PER-CAPITA-LEI-12711"] = "5a1ad80627e354c03e4d6ef776a45db695a1203cea574a288dbcdf706ca58899",
            ["DISTRIB-VAGAS-LEI-12711"] = "0eb12ca67af16ab666e0db0894d795ec725422326cf7dedba2e804f496e0d807",
            ["DISTRIB-VAGAS-INSTITUCIONAL"] = "03b114eb3b559367b7d79f9edb1371f8164c5ede0c5f4b21809ee572c49c9451",
            ["RECONCILIACAO-VAGAS-ART11-PU"] = "ad2d8012ddc1f2ea4d763034899d07590c4a49901744b852dca2e70cede8b1e9",
        };

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
        raiz.TryGetProperty("ato_ancora_codigo", out _).Should().BeTrue("o prazo ancora num ato publicado");
        raiz.TryGetProperty("suspensividade_primeira_instancia", out _).Should().BeTrue();
        raiz.TryGetProperty("suspensividade_segunda_instancia", out _).Should().BeTrue();
        raiz.TryGetProperty("instancias", out _).Should().BeFalse(
            "a lista de instâncias geridas pelo sistema foi descartada");
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

    [Fact(DisplayName = "CA-09 — o hash da regra nova é estável e difere do da regra removida")]
    public void RegraCatalogoSeed_HashDaRegraNova()
    {
        RegraCatalogoSeedItem nova = RegraNova();

        nova.ComputarHash().Should().Be(
            HashRegraNova,
            "a definição do seed deve bater com o literal congelado na migration");
        nova.ComputarHash().Should().NotBe(
            HashRegraRemovida,
            "trocar a definição da regra troca o hash content-addressable");
    }

    [Fact(DisplayName = "CA-04 — as outras 17 regras permanecem idênticas; a substituição toca uma linha")]
    public void DemaisRegras_Inalteradas()
    {
        IReadOnlyList<RegraCatalogoSeedItem> itens = RegraCatalogoSeed.Itens;

        itens.Should().HaveCount(18, "o catálogo continua com 18 linhas (CA-05)");
        itens.Select(i => (i.Codigo, i.Versao)).Should().OnlyHaveUniqueItems("(codigo, versao) é único (CA-05)");

        foreach (RegraCatalogoSeedItem item in itens.Where(i => i.Codigo != CodigoNovo))
        {
            HashesOriginaisDasDemais.Should().ContainKey(
                item.Codigo,
                "nenhuma regra além da substituída pode ter surgido ou mudado de código");
            item.ComputarHash().Should().Be(
                HashesOriginaisDasDemais[item.Codigo],
                $"a regra {item.Codigo} não foi tocada pela substituição");
        }

        // A única linha alterada é a nova — e seu hash não coincide com nenhum dos
        // 17 originais nem com o da regra removida.
        HashesOriginaisDasDemais.Values.Should().NotContain(HashRegraNova);
    }
}
