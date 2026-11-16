namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>A declaração do JSON Schema no builder: ligada, registra com a política do formato; desligada, não faz nada.</summary>
public sealed class SchemaRegistryBuilderJsonSchemaTests
{
    private static readonly System.Reflection.Assembly Montagem = typeof(SchemaRegistryBuilderJsonSchemaTests).Assembly;

    [Fact(DisplayName = "Com o Schema Registry ligado, AddJsonSchema registra a declaração no formato JSON Schema")]
    public void Ligado_RegistraNoFormatoJson()
    {
        ServiceCollection servicos = [];

        servicos.AddSchemaRegistry(Configuracao("http://registry:8081/apis/ccompat/v7")).AddJsonSchema("documento-v1", "Recurso", Montagem);

        SchemaRegistration registro = servicos.BuildServiceProvider().GetRequiredService<SchemaRegistration>();
        registro.Subject.Should().Be("documento-v1");
        registro.Formato.Should().BeSameAs(FormatoDoSchema.JsonSchema);
    }

    [Fact(DisplayName = "Com o Schema Registry desligado, AddJsonSchema não registra nada")]
    public void Desligado_NaoRegistraNada()
    {
        ServiceCollection servicos = [];

        servicos.AddSchemaRegistry(Configuracao(null)).AddJsonSchema("documento-v1", "Recurso", Montagem);

        servicos.Should().NotContain(d => d.ServiceType == typeof(SchemaRegistration));
    }

    [Fact(DisplayName = "AddSchema continua registrando no formato Avro")]
    public void AddSchema_ContinuaAvro()
    {
        ServiceCollection servicos = [];

        servicos.AddSchemaRegistry(Configuracao("http://registry:8081/apis/ccompat/v7")).AddSchema("evento-value", "Recurso", Montagem);

        servicos.BuildServiceProvider().GetRequiredService<SchemaRegistration>().Formato.Should().BeSameAs(FormatoDoSchema.Avro);
    }

    [Theory(DisplayName = "Argumentos vazios ou nulos em AddJsonSchema são recusados")]
    [InlineData("", "Recurso")]
    [InlineData("subject", " ")]
    public void ArgumentosVazios_SaoRecusados(string subject, string recurso)
    {
        SchemaRegistryBuilder builder = new ServiceCollection().AddSchemaRegistry(Configuracao(null));

        Action acao = () => builder.AddJsonSchema(subject, recurso, Montagem);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Assembly nulo em AddJsonSchema é recusado")]
    public void AssemblyNulo_ERecusado()
    {
        SchemaRegistryBuilder builder = new ServiceCollection().AddSchemaRegistry(Configuracao(null));

        Action acao = () => builder.AddJsonSchema("subject", "Recurso", null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    private static IConfiguration Configuracao(string? url) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SchemaRegistry:Url"] = url }).Build();
}
