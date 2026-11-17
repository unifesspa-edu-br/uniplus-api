namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

public sealed class SigaaOptionsValidatorTests
{
    /// <summary>
    /// Cada linha viola uma regra e nada mais além dela: a falha precisa nomear a chave.
    /// </summary>
    public static readonly TheoryData<string, string> ValoresRecusados = new()
    {
        { nameof(SigaaOptions.BaseUrl), "/relativo" },
        { nameof(SigaaOptions.BaseUrl), "http://sigaa.exemplo.test" },
        { nameof(SigaaOptions.Usuario), " " },
        { nameof(SigaaOptions.Senha), "" },
        { nameof(SigaaOptions.ItensPorPagina), "0" },
        { nameof(SigaaOptions.ItensPorPagina), "201" },
        { nameof(SigaaOptions.GrauDeParalelismo), "0" },
        { nameof(SigaaOptions.MaximoDeRetentativas), "-1" },
        { nameof(SigaaOptions.EsperaBaseEntreTentativasEmMilissegundos), "-1" },
        { nameof(SigaaOptions.TimeoutPorTentativaEmSegundos), "0" },
        { nameof(SigaaOptions.TimeoutTotalEmSegundos), "29" },
        { nameof(SigaaOptions.MargemDeRenovacaoDoTokenEmSegundos), "-1" },
        { nameof(SigaaOptions.ValidadeAssumidaDoTokenEmSegundos), "0" },
        { nameof(SigaaOptions.ValidadeMaximaDoTokenEmSegundos), "0" },
        { nameof(SigaaOptions.MargemDeRenovacaoDoTokenEmSegundos), "86400" },
        { nameof(SigaaOptions.MargemDeRenovacaoDoTokenEmSegundos), "3600" },
        { nameof(SigaaOptions.ChamadasMinimasParaAvaliarInterrupcao), "0" },
        { nameof(SigaaOptions.JanelaDeAmostragemDaInterrupcaoEmSegundos), "0" },
        { nameof(SigaaOptions.JanelaDeAmostragemDaInterrupcaoEmSegundos), "59" },
        { nameof(SigaaOptions.ProporcaoDeFalhaParaInterromper), "0" },
        { nameof(SigaaOptions.ProporcaoDeFalhaParaInterromper), "1.5" },
        { nameof(SigaaOptions.DuracaoDaInterrupcaoEmSegundos), "0" },
    };

    public static readonly TheoryData<string, string> ValoresNoLimite = new()
    {
        { nameof(SigaaOptions.ItensPorPagina), "1" },
        { nameof(SigaaOptions.ItensPorPagina), "200" },
        { nameof(SigaaOptions.GrauDeParalelismo), "1" },
        { nameof(SigaaOptions.MaximoDeRetentativas), "0" },
        { nameof(SigaaOptions.EsperaBaseEntreTentativasEmMilissegundos), "0" },
        { nameof(SigaaOptions.TimeoutTotalEmSegundos), "30" },
        { nameof(SigaaOptions.MargemDeRenovacaoDoTokenEmSegundos), "0" },
        { nameof(SigaaOptions.ChamadasMinimasParaAvaliarInterrupcao), "1" },
        { nameof(SigaaOptions.JanelaDeAmostragemDaInterrupcaoEmSegundos), "60" },
        { nameof(SigaaOptions.ProporcaoDeFalhaParaInterromper), "1" },
        { nameof(SigaaOptions.DuracaoDaInterrupcaoEmSegundos), "1" },
    };

    [Fact]
    public void Aceita_configuracao_completa_e_coerente()
    {
        ValidateOptionsResult resultado = Validar(OpcoesDoSigaa.Com());

        resultado.Succeeded.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ValoresRecusados))]
    public void Recusa_o_valor_que_viola_a_regra_e_nomeia_a_chave(string propriedade, string valor)
    {
        ValidateOptionsResult resultado = Validar(OpcoesDoSigaa.Com((propriedade, valor)));

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain($"{SigaaOptions.SectionName}:{propriedade}");
    }

    [Theory]
    [MemberData(nameof(ValoresNoLimite))]
    public void Aceita_o_valor_no_limite_da_regra(string propriedade, string valor)
    {
        ValidateOptionsResult resultado = Validar(OpcoesDoSigaa.Com((propriedade, valor)));

        resultado.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Configuracao_vazia_recusa_na_ordem_em_que_as_regras_sao_declaradas()
    {
        ValidateOptionsResult resultado = Validar(new SigaaOptions { ItensPorPagina = 0 });

        (resultado.Failures ?? []).Select(falha => falha.Split(' ')[0]).Should().Equal(
            "Sigaa:BaseUrl", "Sigaa:Usuario", "Sigaa:Senha", "Sigaa:ItensPorPagina");
    }

    [Theory]
    [InlineData("http://sigaa.exemplo.test")]
    [InlineData("HTTP://sigaa.exemplo.test")]
    public void Recusa_endereco_sem_canal_cifrado(string endereco)
    {
        // A integração transporta senha de serviço, token e CPF. Em canal não cifrado,
        // tudo isso trafega legível.
        ValidateOptionsResult resultado = Validar(OpcoesDoSigaa.Com((nameof(SigaaOptions.BaseUrl), endereco)));

        resultado.FailureMessage.Should().Contain("https");
    }

    [Fact]
    public void Recusa_margem_de_renovacao_que_consome_a_validade_assumida()
    {
        // Com a margem alcançando a validade assumida, todo token obtido quando a
        // expiração não é legível já nasce vencido — e cada chamada ao SIGAA passaria a
        // esperar por uma autenticação nova, uma por chamada.
        ValidateOptionsResult resultado = Validar(OpcoesDoSigaa.Com(
            (nameof(SigaaOptions.ValidadeAssumidaDoTokenEmSegundos), "60"),
            (nameof(SigaaOptions.MargemDeRenovacaoDoTokenEmSegundos), "60")));

        resultado.FailureMessage.Should().Contain("ValidadeAssumida");
    }

    [Fact]
    public void Recusa_janela_da_interrupcao_menor_que_o_dobro_do_limite_por_tentativa()
    {
        ValidateOptionsResult resultado = Validar(OpcoesDoSigaa.Com(
            (nameof(SigaaOptions.TimeoutPorTentativaEmSegundos), "30"),
            (nameof(SigaaOptions.JanelaDeAmostragemDaInterrupcaoEmSegundos), "45")));

        resultado.FailureMessage.Should().Contain("dobro");
    }

    [Fact]
    public void Recusa_senha_ausente_e_diz_de_onde_ela_deveria_vir()
    {
        ValidateOptionsResult resultado = Validar(OpcoesDoSigaa.Com((nameof(SigaaOptions.Senha), "")));

        resultado.FailureMessage.Should().Contain("Sigaa__Senha");
    }

    private static ValidateOptionsResult Validar(SigaaOptions opcoes) =>
        new SigaaOptionsValidator().Validate(name: null, opcoes);
}
