namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O contrato de que a leitura do corpo depende para recusar em 400, nomeando o campo, a união
/// polimórfica cujo documento não declara a variante (issue #1410).
/// </summary>
/// <remarks>
/// <para>
/// A recusa em si é de infraestrutura e está coberta onde ela mora — em
/// <c>PolymorphicDeserializationInputFormatterTests</c>, no envoltório do input formatter, e
/// ponta a ponta em <c>ObrigatoriedadeLegalAdminEndpointTests</c>. O que se fixa aqui são as
/// duas premissas que aquele código consome e que vêm de fora dele: o nome da propriedade
/// discriminadora, que a resposta anuncia ao cliente, e a forma da recusa do desserializador,
/// de onde sai o caminho do campo.
/// </para>
/// <para>
/// Vale para as quatro uniões-raiz do módulo, e não só para as duas que hoje chegam por corpo de
/// comando: o que sustenta a recusa é o contrato de serialização, e ele é o mesmo para as quatro.
/// </para>
/// </remarks>
public sealed class PolymorphicJsonDeserializationTests
{
    public static TheoryData<Type> UnioesRaiz() => new()
    {
        typeof(PredicadoObrigatoriedade),
        typeof(ArgsCriterioDesempate),
        typeof(ArgsRegraAjusteDistribuicao),
        typeof(ArgsRegraEliminacao),
    };

    /// <summary>
    /// O discriminador do projeto é <c>$tipo</c> (ADR-0058), e não o <c>type</c> que a maioria
    /// das bibliotecas usa por convenção — por isso a resposta de recusa o anuncia por extenso
    /// em vez de dizer só que o corpo está errado.
    /// </summary>
    [Theory(DisplayName = "A união declara '$tipo' como propriedade discriminadora")]
    [MemberData(nameof(UnioesRaiz))]
    public void Contrato_UniaoRaiz_DiscriminaPorTipo(Type uniao)
    {
        JsonTypeInfo info = JsonSerializerOptions.Web.GetTypeInfo(uniao);

        info.PolymorphismOptions.Should().NotBeNull()
            .And.Subject.As<JsonPolymorphismOptions>()
            .TypeDiscriminatorPropertyName.Should().Be("$tipo");
    }

    /// <summary>
    /// A recusa que o formatter do framework NÃO converte em erro de model state — é a única do
    /// caminho de leitura reportada como <see cref="NotSupportedException"/>, e é o que fazia
    /// discriminador ausente devolver 500 onde discriminador inválido devolvia 400.
    /// </summary>
    [Theory(DisplayName = "Documento sem discriminador é recusado com NotSupportedException")]
    [MemberData(nameof(UnioesRaiz))]
    public void Desserializar_SemDiscriminador_RecusaComNotSupportedException(Type uniao)
    {
        Action desserializar = () => JsonSerializer.Deserialize("{}", uniao, JsonSerializerOptions.Web);

        desserializar.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// O caminho do erro dentro do documento, de onde sai o nome do campo que a resposta cita.
    /// </summary>
    /// <remarks>
    /// O sufixo é interpolado em código pelo desserializador, e não vem de recurso traduzível —
    /// é a única parte da mensagem que não muda com a cultura, e a única que o envoltório lê. Se
    /// o framework mudar essa forma, a recusa continua saindo em 400, mas deixa de nomear o
    /// campo: é o que este caso acusa antes de o cliente perceber.
    /// </remarks>
    [Theory(DisplayName = "A recusa reporta o caminho do erro dentro do documento")]
    [MemberData(nameof(UnioesRaiz))]
    public void Desserializar_SemDiscriminador_ReportaOCaminho(Type uniao)
    {
        Action desserializar = () => JsonSerializer.Deserialize("{}", uniao, JsonSerializerOptions.Web);

        desserializar.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain(" Path: $ | LineNumber: ");
    }

    /// <summary>
    /// Discriminador desconhecido e raiz não-objeto já eram recusados em 400, porque o
    /// desserializador os reporta como <see cref="JsonException"/> — que o formatter do framework
    /// converte em erro de model state. É a incoerência que a issue #1410 fechou.
    /// </summary>
    [Theory(DisplayName = "Discriminador desconhecido e raiz não-objeto são recusados com JsonException")]
    [MemberData(nameof(UnioesRaiz))]
    public void Desserializar_DiscriminadorDesconhecidoOuRaizNaoObjeto_RecusaComJsonException(Type uniao)
    {
        Action discriminadorDesconhecido = () => JsonSerializer.Deserialize(
            """{"$tipo":"inexistente"}""",
            uniao,
            JsonSerializerOptions.Web);
        Action raizNaoObjeto = () => JsonSerializer.Deserialize("\"lixo\"", uniao, JsonSerializerOptions.Web);

        discriminadorDesconhecido.Should().Throw<JsonException>();
        raizNaoObjeto.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Predicado com '$tipo' e os argumentos da variante é lido como a variante")]
    public void Desserializar_ComDiscriminador_CriaAVariante()
    {
        PredicadoObrigatoriedade? predicado = JsonSerializer.Deserialize<PredicadoObrigatoriedade>(
            """{"$tipo":"etapaObrigatoria","tipoEtapaCodigo":"PROVA_OBJETIVA"}""",
            JsonSerializerOptions.Web);

        predicado.Should().BeOfType<EtapaObrigatoria>()
            .Which.TipoEtapaCodigo.Should().Be("PROVA_OBJETIVA");
    }
}
