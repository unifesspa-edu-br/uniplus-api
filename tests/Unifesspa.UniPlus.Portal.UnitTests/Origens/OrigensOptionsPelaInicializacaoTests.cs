namespace Unifesspa.UniPlus.Portal.UnitTests.Origens;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Portal.Infrastructure.Origens;

public sealed class OrigensOptionsPelaInicializacaoTests
{
    [Theory]
    [InlineData(null, typeof(OptionsValidationException), "Origens:SelecaoBaseUrl é obrigatório.")]
    [InlineData("", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl é obrigatório.")]
    [InlineData("   ", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl é obrigatório.")]
    [InlineData("/relativo", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl precisa ser um endereço http ou https absoluto.")]
    [InlineData("sigaa.exemplo.test", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl precisa ser um endereço http ou https absoluto.")]
    [InlineData("ht!tp://sigaa", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl precisa ser um endereço http ou https absoluto.")]
    [InlineData("ftp://x:1", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl precisa ser um endereço http ou https absoluto.")]
    [InlineData("file:///x", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl precisa ser um endereço http ou https absoluto.")]
    [InlineData("https://[::1", typeof(OptionsValidationException), "Origens:SelecaoBaseUrl precisa ser um endereço http ou https absoluto.")]
    public void Ler_EnderecoInvalido_RecusaComMensagem(string? endereco, Type tipoDeFalha, string mensagem)
    {
        using ServiceProvider provedor = Montar(endereco);

        Action leitura = () => _ = provedor.GetRequiredService<IOptions<OrigensOptions>>().Value;

        Exception? falha = Record.Exception(leitura);

        falha.Should().NotBeNull();
        falha.Should().BeOfType(tipoDeFalha);
        falha.Message.Should().Be(mensagem);
    }

    [Theory]
    [InlineData("http://x:1")]
    [InlineData("http://x/caminho/")]
    public void Ler_EnderecoValido_Aceita(string endereco)
    {
        using ServiceProvider provedor = Montar(endereco);

        Action leitura = () => _ = provedor.GetRequiredService<IOptions<OrigensOptions>>().Value;

        leitura.Should().NotThrow();
    }

    private static ServiceProvider Montar(string? endereco)
    {
        Dictionary<string, string?> valores = new() { ["Origens:PublicacoesBaseUrl"] = "http://x:1" };
        if (endereco is not null)
        {
            valores["Origens:SelecaoBaseUrl"] = endereco;
        }

        IConfiguration configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        ServiceCollection servicos = new();
        servicos.AddPortalOrigens(configuracao);
        return servicos.BuildServiceProvider();
    }
}
