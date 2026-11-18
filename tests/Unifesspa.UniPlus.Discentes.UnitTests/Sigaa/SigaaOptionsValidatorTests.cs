namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using static Unifesspa.UniPlus.Discentes.UnitTests.Sigaa.OpcoesDoSigaa;

public sealed class SigaaOptionsValidatorTests
{
    /// <summary>
    /// Cada linha viola uma regra e nada mais além dela: a falha precisa nomear a chave.
    /// </summary>
    public static readonly TheoryData<string, string> ValoresRecusados = new()
    {
        { ChavesDoSigaa.BaseUrl, "/relativo" },
        { ChavesDoSigaa.BaseUrl, "http://sigaa.exemplo.test" },
        { ChavesDoSigaa.Usuario, " " },
        { ChavesDoSigaa.Senha, "" },
        { ChavesDoSigaa.ItensPorPagina, "0" },
        { ChavesDoSigaa.ItensPorPagina, "201" },
        { ChavesDoSigaa.GrauDeParalelismo, "0" },
        { ChavesDoSigaa.MaximoDeRetentativas, "-1" },
        { ChavesDoSigaa.EsperaBaseEntreTentativas, Segundos(-0.001) },
        { ChavesDoSigaa.TimeoutPorTentativa, Segundos(0) },
        { ChavesDoSigaa.TimeoutTotal, Segundos(29) },
        { ChavesDoSigaa.MargemDeRenovacaoDoToken, Segundos(-1) },
        { ChavesDoSigaa.ValidadeAssumidaDoToken, Segundos(0) },
        { ChavesDoSigaa.ValidadeMaximaDoToken, Segundos(0) },
        { ChavesDoSigaa.MargemDeRenovacaoDoToken, Segundos(86400) },
        { ChavesDoSigaa.MargemDeRenovacaoDoToken, Segundos(3600) },
        { ChavesDoSigaa.ChamadasMinimasParaAvaliarInterrupcao, "0" },
        { ChavesDoSigaa.JanelaDeAmostragemDaInterrupcao, Segundos(0) },
        { ChavesDoSigaa.JanelaDeAmostragemDaInterrupcao, Segundos(59) },
        { ChavesDoSigaa.ProporcaoDeFalhaParaInterromper, "0" },
        { ChavesDoSigaa.ProporcaoDeFalhaParaInterromper, "1.5" },
        { ChavesDoSigaa.DuracaoDaInterrupcao, Segundos(0) },
    };

    public static readonly TheoryData<string, string> ValoresNoLimite = new()
    {
        { ChavesDoSigaa.ItensPorPagina, "1" },
        { ChavesDoSigaa.ItensPorPagina, "200" },
        { ChavesDoSigaa.GrauDeParalelismo, "1" },
        { ChavesDoSigaa.MaximoDeRetentativas, "0" },
        { ChavesDoSigaa.EsperaBaseEntreTentativas, Segundos(0) },
        { ChavesDoSigaa.TimeoutTotal, Segundos(30) },
        { ChavesDoSigaa.MargemDeRenovacaoDoToken, Segundos(0) },
        { ChavesDoSigaa.ChamadasMinimasParaAvaliarInterrupcao, "1" },
        { ChavesDoSigaa.JanelaDeAmostragemDaInterrupcao, Segundos(60) },
        { ChavesDoSigaa.ProporcaoDeFalhaParaInterromper, "1" },
        { ChavesDoSigaa.DuracaoDaInterrupcao, Segundos(1) },
    };

    [Fact]
    public void Aceita_configuracao_completa_e_coerente()
    {
        ValidateOptionsResult resultado = Validar(Com());

        resultado.Succeeded.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ValoresRecusados))]
    public void Recusa_o_valor_que_viola_a_regra_e_nomeia_a_chave(string caminho, string valor)
    {
        ValidateOptionsResult resultado = Validar(Com((caminho, valor)));

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain(Chave(caminho));
    }

    [Theory]
    [MemberData(nameof(ValoresNoLimite))]
    public void Aceita_o_valor_no_limite_da_regra(string caminho, string valor)
    {
        ValidateOptionsResult resultado = Validar(Com((caminho, valor)));

        resultado.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Configuracao_vazia_recusa_na_ordem_em_que_as_regras_sao_declaradas()
    {
        ValidateOptionsResult resultado = Validar(new SigaaOptions { Paginacao = new PaginacaoDoSigaa { ItensPorPagina = 0 } });

        (resultado.Failures ?? []).Select(falha => falha.Split(' ')[0]).Should().Equal(
            "Sigaa:BaseUrl", "Sigaa:Usuario", "Sigaa:Senha", "Sigaa:Paginacao:ItensPorPagina");
    }

    [Theory]
    [InlineData("http://sigaa.exemplo.test")]
    [InlineData("HTTP://sigaa.exemplo.test")]
    public void Recusa_endereco_sem_canal_cifrado(string endereco)
    {
        // A integração transporta senha de serviço, token e CPF. Em canal não cifrado,
        // tudo isso trafega legível.
        ValidateOptionsResult resultado = Validar(Com((ChavesDoSigaa.BaseUrl, endereco)));

        resultado.FailureMessage.Should().Contain("https");
    }

    [Fact]
    public void Recusa_margem_de_renovacao_que_consome_a_validade_assumida()
    {
        // Com a margem alcançando a validade assumida, todo token obtido quando a
        // expiração não é legível já nasce vencido — e cada chamada ao SIGAA passaria a
        // esperar por uma autenticação nova, uma por chamada.
        ValidateOptionsResult resultado = Validar(Com(
            (ChavesDoSigaa.ValidadeAssumidaDoToken, Segundos(60)),
            (ChavesDoSigaa.MargemDeRenovacaoDoToken, Segundos(60))));

        resultado.FailureMessage.Should().Contain("ValidadeAssumida");
    }

    [Fact]
    public void Recusa_janela_da_interrupcao_menor_que_o_dobro_do_limite_por_tentativa()
    {
        ValidateOptionsResult resultado = Validar(Com(
            (ChavesDoSigaa.TimeoutPorTentativa, Segundos(30)),
            (ChavesDoSigaa.JanelaDeAmostragemDaInterrupcao, Segundos(45))));

        resultado.FailureMessage.Should().Contain("dobro");
    }

    [Fact]
    public void Interrupcao_invalida_relata_falhas_em_ordem_sem_repetir_a_janela()
    {
        ValidateOptionsResult resultado = Validar(Com(
            (ChavesDoSigaa.ChamadasMinimasParaAvaliarInterrupcao, "0"),
            (ChavesDoSigaa.JanelaDeAmostragemDaInterrupcao, Segundos(0)),
            (ChavesDoSigaa.ProporcaoDeFalhaParaInterromper, "0"),
            (ChavesDoSigaa.DuracaoDaInterrupcao, Segundos(0))));

        (resultado.Failures ?? []).Select(falha => falha.Split(' ')[0]).Should().Equal(
            Chave(ChavesDoSigaa.ChamadasMinimasParaAvaliarInterrupcao),
            Chave(ChavesDoSigaa.JanelaDeAmostragemDaInterrupcao),
            Chave(ChavesDoSigaa.ProporcaoDeFalhaParaInterromper),
            Chave(ChavesDoSigaa.DuracaoDaInterrupcao));
    }

    [Fact]
    public void Recusa_senha_ausente_e_diz_de_onde_ela_deveria_vir()
    {
        ValidateOptionsResult resultado = Validar(Com((ChavesDoSigaa.Senha, "")));

        resultado.FailureMessage.Should().Contain("Sigaa__Senha");
    }

    private static ValidateOptionsResult Validar(SigaaOptions opcoes) =>
        new SigaaOptionsValidator().Validate(name: null, opcoes);
}
