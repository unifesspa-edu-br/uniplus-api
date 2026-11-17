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

    private static readonly OrigensOptions TodasAsSecoesInvalidas = new()
    {
        SelecaoBaseUrl = "",
        PublicacoesBaseUrl = "ftp://uniplus-api:8080",
        ItensPorPaginaDeAtos = 0,
        LimiteDePaginasDeAtos = 0,
        TimeoutPorTentativaEmSegundos = 0,
        TimeoutTotalEmSegundos = 0,
        MaximoDeRetentativas = -1,
        EsperaBaseEntreTentativasEmMilissegundos = -1,
        ChamadasMinimasParaAvaliarInterrupcao = 1,
        JanelaDeAmostragemDasFalhasEmSegundos = -1,
        ProporcaoDeFalhaParaInterromper = 0,
        DuracaoDaInterrupcaoEmSegundos = 0,
        ReencaminharAposEmSegundos = 0,
    };

    private static readonly string[] MensagensDeTodasAsSecoesEmOrdem =
    [
        "Origens:SelecaoBaseUrl é obrigatório.",
        "Origens:PublicacoesBaseUrl precisa ser um endereço http ou https absoluto.",
        "Origens:ItensPorPaginaDeAtos precisa estar entre 1 e 100, que é o limite imposto pela origem.",
        "Origens:LimiteDePaginasDeAtos precisa ser pelo menos 1.",
        "Origens:TimeoutPorTentativaEmSegundos precisa ser pelo menos 1.",
        "Origens:TimeoutTotalEmSegundos precisa ser maior que TimeoutPorTentativaEmSegundos; do contrário nenhuma retentativa cabe no limite da consulta.",
        "Origens:MaximoDeRetentativas não pode ser negativo.",
        "Origens:EsperaBaseEntreTentativasEmMilissegundos não pode ser negativa.",
        "Origens:ChamadasMinimasParaAvaliarInterrupcao precisa ser pelo menos 2.",
        "Origens:JanelaDeAmostragemDasFalhasEmSegundos precisa ser pelo menos o dobro de TimeoutPorTentativaEmSegundos.",
        "Origens:ProporcaoDeFalhaParaInterromper precisa estar entre 0 (exclusivo) e 1.",
        "Origens:DuracaoDaInterrupcaoEmSegundos precisa ser pelo menos 1.",
        "Origens:ReencaminharAposEmSegundos precisa ser pelo menos 1.",
    ];

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
    public void Recusa_pagina_de_atos_acima_do_limite_da_origem()
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

    [Fact]
    public void Configuracao_com_todas_as_secoes_invalidas_devolve_todas_as_mensagens_em_ordem_e_texto_exato()
    {
        ValidateOptionsResult resultado = new OrigensOptionsValidator().Validate(null, TodasAsSecoesInvalidas);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().Equal(MensagensDeTodasAsSecoesEmOrdem);
    }
}
