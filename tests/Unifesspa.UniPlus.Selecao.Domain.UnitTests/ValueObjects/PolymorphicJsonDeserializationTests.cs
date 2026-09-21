namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class PolymorphicJsonDeserializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> RootPolymorphicTypes()
    {
        yield return [typeof(PredicadoObrigatoriedade)];
        yield return [typeof(ArgsCriterioDesempate)];
        yield return [typeof(ArgsRegraAjusteDistribuicao)];
        yield return [typeof(ArgsRegraEliminacao)];
    }

    [Theory(DisplayName = "Union polimórfica sem discriminator rejeita payload com NotSupportedException")]
    [MemberData(nameof(RootPolymorphicTypes))]
    public void Deserialize_DiscriminatorAusente_RejeitaComNotSupportedException(Type tipo)
    {
        Action desserializar = () => JsonSerializer.Deserialize("{}", tipo, Options);

        desserializar.Should().Throw<NotSupportedException>();
    }

    [Theory(DisplayName = "Union polimórfica com discriminator desconhecido rejeita payload com JsonException")]
    [MemberData(nameof(RootPolymorphicTypes))]
    public void Deserialize_DiscriminatorDesconhecido_RejeitaComJsonException(Type tipo)
    {
        Action desserializar = () => JsonSerializer.Deserialize(
            """{"$tipo":"inexistente"}""",
            tipo,
            Options);

        desserializar.Should().Throw<JsonException>();
    }

    [Theory(DisplayName = "Union polimórfica em raiz não-objeto rejeita payload com JsonException")]
    [MemberData(nameof(RootPolymorphicTypes))]
    public void Deserialize_RaizNaoObjeto_RejeitaComJsonException(Type tipo)
    {
        Action desserializar = () => JsonSerializer.Deserialize(
            "\"lixo\"",
            tipo,
            Options);

        desserializar.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Predicado com $tipo e argumentos da variante é desserializado corretamente")]
    public void Deserialize_PredicadoEtapaObrigatoriaComDiscriminator_CriaVarianteEsperada()
    {
        PredicadoObrigatoriedade? predicado = JsonSerializer.Deserialize<PredicadoObrigatoriedade>(
            """{"$tipo":"etapaObrigatoria","tipoEtapaCodigo":"PROVA_OBJETIVA"}""",
            Options);

        predicado.Should().BeOfType<EtapaObrigatoria>()
            .Which.TipoEtapaCodigo.Should().Be("PROVA_OBJETIVA");
    }
}
