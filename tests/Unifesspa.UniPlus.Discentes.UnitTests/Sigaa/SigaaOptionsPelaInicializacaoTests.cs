namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

/// <summary>
/// A configuração do SIGAA pelo caminho que o processo percorre: o registro lê as chaves,
/// o binder as converte e o validador acumula as recusas.
/// </summary>
public sealed class SigaaOptionsPelaInicializacaoTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_endereco_a_integracao_nao_e_registrada_nem_validada(string endereco)
    {
        using ServiceProvider provedor = Registrar(endereco, senha: "");

        provedor.GetService<IOptions<SigaaOptions>>().Should().BeNull();
    }

    [Fact]
    public void Chave_ausente_nao_registra_a_integracao()
    {
        ServiceCollection servicos = new();
        servicos.AddSigaaVinculoDiscenteClient(new ConfigurationBuilder().Build());
        using ServiceProvider provedor = servicos.BuildServiceProvider();

        provedor.GetService<IOptions<SigaaOptions>>().Should().BeNull();
    }

    /// <summary>
    /// Um caminho relativo, como <c>/relativo</c>, chega do binder como endereço relativo e é
    /// recusado por não ser absoluto; a credencial ausente é acumulada na mesma mensagem.
    /// </summary>
    [Theory]
    [InlineData("/relativo")]
    [InlineData("sigaa.exemplo.test")]
    public void Endereco_que_nao_e_absoluto_acumula_com_a_credencial_ausente(string endereco)
    {
        using ServiceProvider provedor = Registrar(endereco, senha: "");

        string[] falhas = FalhasAoLer(provedor);

        falhas.Should().HaveCount(2);
        falhas[0].Should().Be("Sigaa:BaseUrl precisa ser um endereço absoluto.");
        falhas[1].Should().StartWith("Sigaa:Senha é obrigatório");
    }

    [Fact]
    public void Endereco_sem_canal_cifrado_acumula_com_a_credencial_ausente()
    {
        using ServiceProvider provedor = Registrar("http://sigaa.exemplo.test", senha: "");

        string[] falhas = FalhasAoLer(provedor);

        falhas.Should().HaveCount(2);
        falhas[0].Should().StartWith("Sigaa:BaseUrl precisa usar https.");
        falhas[1].Should().StartWith("Sigaa:Senha é obrigatório");
    }

    [Fact]
    public void Endereco_impossivel_e_recusado_junto_da_credencial_ausente()
    {
        using ServiceProvider provedor = Registrar("ht!tp://sigaa", senha: "");

        string[] falhas = FalhasAoLer(provedor);

        falhas.Should().HaveCount(2);
        falhas[0].Should().Be("Sigaa:BaseUrl precisa ser um endereço absoluto.");
        falhas[1].Should().StartWith("Sigaa:Senha é obrigatório");
    }

    /// <summary>
    /// Um endereço que nem como URI se lê (como <c>https://[::1</c>) estoura no binder, antes
    /// do validador, e a falha nomeia a chave <c>Sigaa:BaseUrl</c>. A recusa da senha só
    /// aparece depois que o operador corrige o endereço.
    /// </summary>
    [Fact]
    public void Endereco_mal_formado_de_verdade_falha_no_binder_nomeando_a_chave()
    {
        using ServiceProvider provedor = Registrar("https://[::1", senha: "");

        Func<SigaaOptions> leitura = () => provedor.GetRequiredService<IOptions<SigaaOptions>>().Value;

        leitura.Should().Throw<InvalidOperationException>().WithMessage("*Sigaa:BaseUrl*");
    }

    [Fact]
    public void Configuracao_valida_e_lida_sem_recusa()
    {
        using ServiceProvider provedor = Registrar("https://sigaa.exemplo.test", senha: "segredo");

        SigaaOptions opcoes = provedor.GetRequiredService<IOptions<SigaaOptions>>().Value;

        opcoes.Credenciais.Usuario.Should().Be("servico");
    }

    private static ServiceProvider Registrar(string endereco, string senha)
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [OpcoesDoSigaa.Chave(ChavesDoSigaa.BaseUrl)] = endereco,
                [OpcoesDoSigaa.Chave(ChavesDoSigaa.Usuario)] = "servico",
                [OpcoesDoSigaa.Chave(ChavesDoSigaa.Senha)] = senha,
            })
            .Build();

        ServiceCollection servicos = new();
        servicos.AddSigaaVinculoDiscenteClient(configuracao);

        return servicos.BuildServiceProvider();
    }

    private static string[] FalhasAoLer(IServiceProvider provedor)
    {
        Func<SigaaOptions> leitura = () => provedor.GetRequiredService<IOptions<SigaaOptions>>().Value;

        return [.. leitura.Should().Throw<OptionsValidationException>().Which.Failures];
    }
}
