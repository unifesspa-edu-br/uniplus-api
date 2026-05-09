namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public sealed class KafkaSettingsValidatorTests
{
    private static KafkaSettings Bind(Dictionary<string, string?> values)
    {
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return cfg.GetSection(KafkaSettings.SectionName).Get<KafkaSettings>() ?? new KafkaSettings();
    }

    [Fact]
    public void Validate_BootstrapVazio_DeveSerSucesso()
    {
        // Transporte desligado em Development sem Kafka — não há o que validar.
        KafkaSettings settings = new();
        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_PlaintextSemSecurity_DeveSerSucesso()
    {
        // docker-compose dev: só BootstrapServers, sem SecurityProtocol (PLAINTEXT default).
        KafkaSettings settings = new() { BootstrapServers = "kafka:9092" };
        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("PLAINTEXT")]
    [InlineData("Plaintext")]
    [InlineData("plaintext")]
    public void Validate_SecurityProtocolPlaintext_DeveSerSucesso(string raw)
    {
        KafkaSettings settings = new() { BootstrapServers = "kafka:9092", SecurityProtocol = raw };
        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_SecurityProtocolInvalido_DeveFalhar()
    {
        KafkaSettings settings = new() { BootstrapServers = "kafka:9092", SecurityProtocol = "MTLS" };
        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kafka:SecurityProtocol 'MTLS'");
    }

    [Theory]
    [InlineData("SASL_SSL")]
    [InlineData("SaslSsl")]
    [InlineData("sasl_ssl")]
    public void Validate_SaslSslCompleto_DeveSerSucesso(string protocolRaw)
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = protocolRaw,
            SaslMechanism = "SCRAM-SHA-512",
            SaslUsername = "uniplus",
            SaslPassword = "secret",
            SslCaLocation = "/etc/uniplus-kafka/ca.crt",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_SaslSslSemUsername_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "SCRAM-SHA-512",
            SaslPassword = "secret",
            SslCaLocation = "/etc/uniplus-kafka/ca.crt",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kafka:SaslUsername");
    }

    [Fact]
    public void Validate_SaslSslSemPassword_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "SCRAM-SHA-512",
            SaslUsername = "uniplus",
            SslCaLocation = "/etc/uniplus-kafka/ca.crt",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kafka:SaslPassword");
    }

    [Fact]
    public void Validate_SaslSslSemMecanismo_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslUsername = "uniplus",
            SaslPassword = "secret",
            SslCaLocation = "/etc/uniplus-kafka/ca.crt",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kafka:SaslMechanism");
    }

    [Fact]
    public void Validate_SaslSslMecanismoInvalido_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "DIGEST-MD5",
            SaslUsername = "u",
            SaslPassword = "p",
            SslCaLocation = "/etc/ca.crt",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("'DIGEST-MD5'");
    }

    [Theory]
    [InlineData("OAUTHBEARER")]
    [InlineData("GSSAPI")]
    public void Validate_SaslSslMecanismoNaoSuportadoPeloUniPlus_DeveFalhar(string mechanism)
    {
        // OAUTHBEARER e GSSAPI parseiam no Confluent.Kafka mas não são suportados pelo helper —
        // operador deve estender o caminho explicitamente, sem cair em fallback inseguro.
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = mechanism,
            SslCaLocation = "/etc/ca.crt",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("não é suportado pelo Uni+");
    }

    [Fact]
    public void Validate_CamposSaslComProtocoloPlaintext_DeveFalhar()
    {
        // Risco: SASL_username preenchido com SecurityProtocol vazio → cliente roda PLAINTEXT
        // mas operador acreditando que está autenticando. Validator precisa cortar isso.
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SaslUsername = "uniplus",
            SaslPassword = "secret",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SASL_PLAINTEXT");
    }

    [Fact]
    public void Validate_CamposSslComProtocoloPlaintext_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SslCaLocation = "/etc/ca.crt",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SecurityProtocol=SSL");
    }

    [Fact]
    public void Validate_CaLocationECaPemSimultaneos_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "SCRAM-SHA-512",
            SaslUsername = "u",
            SaslPassword = "p",
            SslCaLocation = "/etc/ca.crt",
            SslCaPem = "-----BEGIN CERTIFICATE-----\nMIIB...",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("mutuamente exclusivos");
    }

    [Fact]
    public void Validate_SaslSslSemCaLocationENemPem_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "SCRAM-SHA-512",
            SaslUsername = "u",
            SaslPassword = "p",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kafka:SslCaLocation");
    }

    [Fact]
    public void Validate_SaslSslComCaPem_DeveSerSucesso()
    {
        // SslCaPem é alternativa válida a SslCaLocation.
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "SCRAM-SHA-512",
            SaslUsername = "u",
            SaslPassword = "p",
            SslCaPem = "-----BEGIN CERTIFICATE-----\nMIIB...\n-----END CERTIFICATE-----",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_SaslPlaintextSemCa_DeveSerSucesso()
    {
        // SASL_PLAINTEXT não exige CA — apenas SASL.
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_PLAINTEXT",
            SaslMechanism = "PLAIN",
            SaslUsername = "u",
            SaslPassword = "p",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_SslPuroSemCa_DeveFalhar()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SSL",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kafka:SslCaLocation");
    }

    [Fact]
    public void Validate_SaslSslComMultiplosCamposFaltando_DeveAcumularFalhas()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
        };

        ValidateOptionsResult result = new KafkaSettingsValidator().Validate(name: null, settings);
        result.Failed.Should().BeTrue();
        result.Failures.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Bind_AceitaTodasAsPropriedades()
    {
        Dictionary<string, string?> values = new()
        {
            ["Kafka:BootstrapServers"] = "k1:9092,k2:9092",
            ["Kafka:SecurityProtocol"] = "SASL_SSL",
            ["Kafka:SaslMechanism"] = "SCRAM-SHA-512",
            ["Kafka:SaslUsername"] = "uniplus-portal",
            ["Kafka:SaslPassword"] = "p455",
            ["Kafka:SslCaLocation"] = "/etc/uniplus-kafka/ca.crt",
            ["Kafka:SslCaPem"] = "-----BEGIN CERTIFICATE-----\nMIIB...",
        };

        KafkaSettings settings = Bind(values);

        settings.BootstrapServers.Should().Be("k1:9092,k2:9092");
        settings.SecurityProtocol.Should().Be("SASL_SSL");
        settings.SaslMechanism.Should().Be("SCRAM-SHA-512");
        settings.SaslUsername.Should().Be("uniplus-portal");
        settings.SaslPassword.Should().Be("p455");
        settings.SslCaLocation.Should().Be("/etc/uniplus-kafka/ca.crt");
        settings.SslCaPem.Should().StartWith("-----BEGIN CERTIFICATE-----");
    }
}
