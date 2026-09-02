namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

public sealed class SigaaOptionsValidatorTests
{
    [Fact]
    public void Aceita_configuracao_completa_e_coerente()
    {
        ValidateOptionsResult resultado = Validar(Basicas());

        resultado.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://sigaa.exemplo.test")]
    [InlineData("HTTP://sigaa.exemplo.test")]
    public void Recusa_endereco_sem_canal_cifrado(string endereco)
    {
        // A integração transporta senha de serviço, token e CPF. Em canal não cifrado,
        // tudo isso trafega legível.
        ValidateOptionsResult resultado = Validar(Basicas(baseUrl: endereco));

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("https");
    }

    [Fact]
    public void Recusa_margem_de_renovacao_que_consome_a_validade_assumida()
    {
        // Com a margem alcançando a validade assumida, todo token obtido quando a
        // expiração não é legível já nasce vencido — e cada chamada ao SIGAA passaria a
        // esperar por uma autenticação nova, uma por chamada.
        ValidateOptionsResult resultado = Validar(
            Basicas(validadeAssumida: 60, margemDeRenovacao: 60));

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("ValidadeAssumida");
    }

    [Fact]
    public void Recusa_janela_do_corte_menor_que_o_dobro_do_limite_por_tentativa()
    {
        ValidateOptionsResult resultado = Validar(
            Basicas(timeoutPorTentativa: 30, janelaDoCorte: 45));

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("dobro");
    }

    [Fact]
    public void Recusa_senha_ausente_e_diz_de_onde_ela_deveria_vir()
    {
        ValidateOptionsResult resultado = Validar(Basicas(senha: ""));

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("Sigaa__Senha");
    }

    [Fact]
    public void Recusa_pagina_maior_que_o_teto_da_origem()
    {
        ValidateOptionsResult resultado = Validar(
            Basicas(itensPorPagina: SigaaOptions.MaximoItensPorPagina + 1));

        resultado.Failed.Should().BeTrue();
    }

    /// <summary>
    /// Configuração válida, com um campo por vez substituído pelo que o teste quer provar.
    /// </summary>
    private static SigaaOptions Basicas(
        string baseUrl = "https://sigaa.exemplo.test",
        string senha = "segredo",
        int itensPorPagina = SigaaOptions.MaximoItensPorPagina,
        int validadeAssumida = 3600,
        int margemDeRenovacao = 60,
        int timeoutPorTentativa = 30,
        int janelaDoCorte = 120) => new()
        {
            BaseUrl = baseUrl,
            Usuario = "servico",
            Senha = senha,
            ItensPorPagina = itensPorPagina,
            ValidadeAssumidaDoTokenEmSegundos = validadeAssumida,
            MargemDeRenovacaoDoTokenEmSegundos = margemDeRenovacao,
            TimeoutPorTentativaEmSegundos = timeoutPorTentativa,
            JanelaDeAmostragemDoCorteEmSegundos = janelaDoCorte,
        };

    private static ValidateOptionsResult Validar(SigaaOptions opcoes) =>
        new SigaaOptionsValidator().Validate(name: null, opcoes);
}
