namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Wolverine.Runtime;
using Wolverine.Runtime.Serialization;

/// <summary>
/// O código de contrato dos enums nas mensagens do Host e do Portal, pelo serializador que a configuração do backbone
/// Wolverine (<see cref="WolverineOutboxConfiguration"/>) entrega ao host construído sem iniciar.
/// </summary>
public sealed class SerializacaoDosCodigosNasMensagensDoHostTests
{
    private const string NomeDaConnectionString = "UniPlusDb";

    private static readonly IMessageSerializer SerializadorDoBackbone = ResolverODoBackbone();

    private readonly CasosDaSerializacaoDosCodigosNasMensagens _casos = new(SerializadorDoBackbone);

    [Theory(DisplayName = "A mensagem é escrita com os códigos declarados")]
    [MemberData(nameof(CasosDaSerializacaoDosCodigosNasMensagens.Escritas), MemberType = typeof(CasosDaSerializacaoDosCodigosNasMensagens))]
    public void Escrever_MensagemComCodigo_EscreveOTextoEsperado(object mensagem, string esperado) =>
        _casos.Escrever(mensagem).Should().Be(esperado);

    [Theory(DisplayName = "A mensagem escrita com códigos volta igual na leitura")]
    [MemberData(nameof(CasosDaSerializacaoDosCodigosNasMensagens.IdasEVoltas), MemberType = typeof(CasosDaSerializacaoDosCodigosNasMensagens))]
    public void IdaEVolta_MensagemComCodigo_EscreveOTextoEsperadoEVoltaIgual(object original, string esperado)
    {
        ArgumentNullException.ThrowIfNull(original);
        string texto = _casos.Escrever(original);
        texto.Should().Be(esperado);
        _casos.Ler(original.GetType(), texto).Should().BeEquivalentTo(original);
    }

    [Theory(DisplayName = "O código declarado volta como o membro que o declara")]
    [MemberData(nameof(CasosDaSerializacaoDosCodigosNasMensagens.CodigosDeclarados), MemberType = typeof(CasosDaSerializacaoDosCodigosNasMensagens))]
    public void Ler_CodigoDeclarado_DevolveOMembro(string codigo, ModoDeEntregaDeTeste esperado) =>
        _casos.Ler<MensagemComModo>($$"""{"modo":"{{codigo}}"}""").Modo.Should().Be(esperado);

    [Theory(DisplayName = "Texto que não é código é recusado sem repetir o valor")]
    [MemberData(nameof(CasosDaSerializacaoDosCodigosNasMensagens.TextosQueNaoSaoCodigo), MemberType = typeof(CasosDaSerializacaoDosCodigosNasMensagens))]
    public void Ler_TextoQueNaoECodigo_LancaJsonExceptionSemRepetirOValor(string texto)
    {
        Action ler = () => _casos.Ler<MensagemComModo>($$"""{"modo":"{{texto}}"}""");
        ler.Should().Throw<JsonException>().Which.Message.Should().NotContain(texto);
    }

    [Theory(DisplayName = "O corpo com código inválido é recusado")]
    [MemberData(nameof(CasosDaSerializacaoDosCodigosNasMensagens.CorposRecusados), MemberType = typeof(CasosDaSerializacaoDosCodigosNasMensagens))]
    public void Ler_CorpoComCodigoInvalido_LancaJsonException(Type tipo, string corpo)
    {
        Action ler = () => _casos.Ler(tipo, corpo);
        ler.Should().Throw<JsonException>();
    }

    // A construção do host não conecta ao banco: a connection string só precisa existir.
    private static IMessageSerializer ResolverODoBackbone()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{NomeDaConnectionString}"] = "Host=localhost;Port=5432;Database=x;Username=u;Password=p",
            })
            .Build();

        using IHost host = Host.CreateDefaultBuilder()
            .UseWolverineOutboxCascading(configuracao, NomeDaConnectionString)
            .Build();

        return host.Services.GetRequiredService<IWolverineRuntime>().Options.DefaultSerializer;
    }
}
