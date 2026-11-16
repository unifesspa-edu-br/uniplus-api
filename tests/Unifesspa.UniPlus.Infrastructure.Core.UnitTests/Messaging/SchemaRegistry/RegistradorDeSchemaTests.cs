namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System.Net;
using System.Net.Sockets;

using AwesomeAssertions;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using SrSchema = Confluent.SchemaRegistry.Schema;

/// <summary>
/// O registro único de schemas: o tipo declarado vai ao Schema Registry, o JSON Schema só registra com a regra NONE no
/// subject, e só a indisponibilidade do Schema Registry vira resultado; o resto propaga.
/// </summary>
public sealed class RegistradorDeSchemaTests
{
    private const string Subject = "documento-v1";
    private const string RecursoJson = "Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry.Resources.DocumentoDeTeste.schema.json";
    private const string RecursoJsonMalformado = "Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry.Resources.DocumentoMalformado.schema.json";
    private const string RecursoAvro = "Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry.Resources.TestSchema.avsc";

    private readonly ISchemaRegistryClient _schemaRegistry = Substitute.For<ISchemaRegistryClient>();
    private readonly ILeitorDaRegraDeCompatibilidadeDoSubject _leitorDaRegra = Substitute.For<ILeitorDaRegraDeCompatibilidadeDoSubject>();
    private readonly RegistradorDeSchema _registrador;

    public RegistradorDeSchemaTests()
    {
        _schemaRegistry.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true).Returns(7);
        _leitorDaRegra.LerRegraDeCompatibilidadeEfetivaAsync(Subject, Arg.Any<CancellationToken>()).Returns(Compatibility.None);
        _registrador = new RegistradorDeSchema(_schemaRegistry, _leitorDaRegra, NullLogger<RegistradorDeSchema>.Instance);
    }

    [Fact(DisplayName = "JSON Schema é registrado com o tipo JSON, depois de conferir a regra NONE do subject")]
    public async Task JsonSchema_RegistraComTipoJson()
    {
        ResultadoDoRegistroDeSchema resultado = await _registrador.RegistrarAsync(Json(RecursoJson), default);

        resultado.Should().Be(ResultadoDoRegistroDeSchema.Confirmado);
        await _schemaRegistry.Received(1).RegisterSchemaAsync(
            Subject, Arg.Is<SrSchema>(schema => schema.SchemaType == SchemaType.Json), normalize: true);
        await _leitorDaRegra.Received(1).LerRegraDeCompatibilidadeEfetivaAsync(Subject, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Avro segue como era: tipo Avro e nenhuma leitura da regra do subject")]
    public async Task Avro_RegistraComTipoAvro_SemLerARegra()
    {
        SchemaRegistration avro = new(Subject, RecursoAvro, typeof(RegistradorDeSchemaTests).Assembly);

        await _registrador.RegistrarAsync(avro, default);

        await _schemaRegistry.Received(1).RegisterSchemaAsync(
            Subject, Arg.Is<SrSchema>(schema => schema.SchemaType == SchemaType.Avro), normalize: true);
        await _leitorDaRegra.DidNotReceiveWithAnyArgs().LerRegraDeCompatibilidadeEfetivaAsync(default!, default);
    }

    [Theory(DisplayName = "Regra do subject diferente de NONE trava o registro, com o subject e as duas regras na mensagem")]
    [InlineData(Compatibility.Backward)]
    [InlineData(Compatibility.BackwardTransitive)]
    [InlineData(Compatibility.Full)]
    public async Task RegraDiferenteDeNone_FalhaSemRegistrar(Compatibility encontrada)
    {
        _leitorDaRegra.LerRegraDeCompatibilidadeEfetivaAsync(Subject, Arg.Any<CancellationToken>()).Returns(encontrada);

        Func<Task> acao = () => _registrador.RegistrarAsync(Json(RecursoJson), default);

        (await acao.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(Subject).And.Contain(encontrada.ToString()).And.Contain("None");
        await _schemaRegistry.DidNotReceiveWithAnyArgs().RegisterSchemaAsync(default!, default(SrSchema)!, default);
    }

    [Fact(DisplayName = "JSON que não é bem formado falha antes de ir ao Schema Registry, citando o recurso")]
    public async Task JsonMalformado_FalhaAntesDeIrAoRegistry()
    {
        Func<Task> acao = () => _registrador.RegistrarAsync(Json(RecursoJsonMalformado), default);

        (await acao.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("DocumentoMalformado");
        await _schemaRegistry.DidNotReceiveWithAnyArgs().RegisterSchemaAsync(default!, default(SrSchema)!, default);
        await _leitorDaRegra.DidNotReceiveWithAnyArgs().LerRegraDeCompatibilidadeEfetivaAsync(default!, default);
    }

    [Fact(DisplayName = "Recurso ausente falha citando o tipo do schema")]
    public async Task RecursoAusente_CitaOTipo()
    {
        SchemaRegistration ausente = new(Subject, "Nao.Existe.schema.json", typeof(RegistradorDeSchemaTests).Assembly)
        {
            Formato = FormatoDoSchema.JsonSchema,
        };

        Func<Task> acao = () => _registrador.RegistrarAsync(ausente, default);

        (await acao.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("JSON Schema");
    }

    [Fact(DisplayName = "Schema Registry indisponível ao registrar vira resultado, sem lançar")]
    public async Task RegistryIndisponivel_ViraResultado()
    {
        _schemaRegistry.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(new SocketException((int)SocketError.ConnectionRefused));

        ResultadoDoRegistroDeSchema resultado = await _registrador.RegistrarAsync(Json(RecursoJson), default);

        resultado.Should().Be(ResultadoDoRegistroDeSchema.SchemaRegistryIndisponivel);
    }

    [Fact(DisplayName = "Schema Registry indisponível ao ler a regra também vira resultado, e nada é registrado")]
    public async Task RegistryIndisponivelNaLeituraDaRegra_ViraResultado()
    {
        _leitorDaRegra.LerRegraDeCompatibilidadeEfetivaAsync(Subject, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException(HttpRequestError.ConnectionError, "recusada"));

        ResultadoDoRegistroDeSchema resultado = await _registrador.RegistrarAsync(Json(RecursoJson), default);

        resultado.Should().Be(ResultadoDoRegistroDeSchema.SchemaRegistryIndisponivel);
        await _schemaRegistry.DidNotReceiveWithAnyArgs().RegisterSchemaAsync(default!, default(SrSchema)!, default);
    }

    [Fact(DisplayName = "Falha determinística ao ler a regra (credencial recusada) propaga")]
    public async Task FalhaDeterministicaNaLeituraDaRegra_Propaga()
    {
        _leitorDaRegra.LerRegraDeCompatibilidadeEfetivaAsync(Subject, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("401", inner: null, HttpStatusCode.Unauthorized));

        Func<Task> acao = () => _registrador.RegistrarAsync(Json(RecursoJson), default);

        await acao.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact(DisplayName = "Conflito de compatibilidade no registro propaga")]
    public async Task ConflitoDeCompatibilidade_Propaga()
    {
        _schemaRegistry.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(new SchemaRegistryException("incompatível", HttpStatusCode.Conflict, errorCode: 409));

        Func<Task> acao = () => _registrador.RegistrarAsync(Json(RecursoJson), default);

        await acao.Should().ThrowAsync<SchemaRegistryException>();
    }

    private static SchemaRegistration Json(string recurso) =>
        new(Subject, recurso, typeof(RegistradorDeSchemaTests).Assembly) { Formato = FormatoDoSchema.JsonSchema };
}
