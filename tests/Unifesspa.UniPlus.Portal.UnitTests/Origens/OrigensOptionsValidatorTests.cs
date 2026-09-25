namespace Unifesspa.UniPlus.Portal.UnitTests.Origens;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Portal.Infrastructure.Origens;

public sealed class OrigensOptionsValidatorTests
{
    private static readonly OrigensOptions Valida = new()
    {
        SelecaoBaseUrl = "http://uniplus-api:8080",
        PublicacoesBaseUrl = "http://uniplus-api:8080",
    };

    [Fact]
    public void Aceita_enderecos_http_internos_com_os_valores_padrao()
    {
        new OrigensOptionsValidator().Validate(null, Valida).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Recusa_origem_de_atos_sem_endereco()
    {
        ValidateOptionsResult resultado = new OrigensOptionsValidator().Validate(
            null,
            new OrigensOptions { SelecaoBaseUrl = Valida.SelecaoBaseUrl });

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("PublicacoesBaseUrl");
    }

    [Fact]
    public void Recusa_limite_total_que_nao_comporta_retentativa()
    {
        ValidateOptionsResult resultado = new OrigensOptionsValidator().Validate(
            null,
            new OrigensOptions
            {
                SelecaoBaseUrl = Valida.SelecaoBaseUrl,
                PublicacoesBaseUrl = Valida.PublicacoesBaseUrl,
                TimeoutPorTentativaEmSegundos = 5,
                TimeoutTotalEmSegundos = 5,
            });

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("TimeoutTotalEmSegundos");
    }

    [Fact]
    public void Recusa_pagina_de_atos_acima_do_teto_da_origem()
    {
        ValidateOptionsResult resultado = new OrigensOptionsValidator().Validate(
            null,
            new OrigensOptions
            {
                SelecaoBaseUrl = Valida.SelecaoBaseUrl,
                PublicacoesBaseUrl = Valida.PublicacoesBaseUrl,
                ItensPorPaginaDeAtos = OrigensOptions.MaximoItensPorPaginaDeAtos + 1,
            });

        resultado.Failed.Should().BeTrue();
    }

    [Fact]
    public void Recusa_indicacao_de_reencaminhamento_menor_que_um_segundo()
    {
        ValidateOptionsResult resultado = new OrigensOptionsValidator().Validate(
            null,
            new OrigensOptions
            {
                SelecaoBaseUrl = Valida.SelecaoBaseUrl,
                PublicacoesBaseUrl = Valida.PublicacoesBaseUrl,
                ReencaminharAposEmSegundos = 0,
            });

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("ReencaminharAposEmSegundos");
    }
}
