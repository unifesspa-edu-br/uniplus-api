namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.Middleware;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Wolverine;

/// <summary>Origem do identificador de correlação quando o header do envelope não serve.</summary>
public sealed class CorrelationIdEnvelopeMiddlewareOrigemTests
{
    private const string HeaderDoEnvelope = CorrelationIdEnvelopeMiddleware.HeaderName;

    private static string Resolver(string? header, string? ambiente)
    {
        ICorrelationIdAccessor acessor = Substitute.For<ICorrelationIdAccessor>();
        acessor.CorrelationId.Returns(ambiente);
        Envelope envelope = new();
        if (header is not null)
        {
            envelope.Headers[HeaderDoEnvelope] = header;
        }

        using IDisposable escopo = CorrelationIdEnvelopeMiddleware.Before(envelope, acessor);
        return envelope.Headers[HeaderDoEnvelope]!;
    }

    [Theory]
    [InlineData("")]
    [InlineData("com espaco")]
    public void Before_HeaderVazioOuInvalidoComAmbienteValido_UsaOAmbiente(string header)
    {
        Resolver(header, "req-ambiente-1").Should().Be("req-ambiente-1");
    }

    [Fact]
    public void Before_HeaderEAmbienteInvalidos_GeraGuidNoFormatoD()
    {
        string gerado = Resolver("com espaco", "ambiente invalido");

        gerado.Should().MatchRegex("^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$");
    }

    [Fact]
    public void Before_AmbienteVazioSemHeader_GeraGuid()
    {
        Guid.TryParse(Resolver(header: null, ambiente: ""), out _).Should().BeTrue();
    }

    [Fact]
    public void Before_HeaderValidoNoLimiteDeTamanho_Preserva()
    {
        string noLimite = new('a', CorrelationIdEnvelopeMiddleware.MaxCorrelationIdLength);

        Resolver(noLimite, "req-ambiente-1").Should().Be(noLimite);
    }
}
