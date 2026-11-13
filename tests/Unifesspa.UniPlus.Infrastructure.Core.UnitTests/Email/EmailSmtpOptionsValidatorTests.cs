namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Email;

public sealed class EmailSmtpOptionsValidatorTests
{
    private readonly EmailSmtpOptionsValidator _validator = new();

    [Fact(DisplayName = "Remetente que não é endereço de e-mail é recusado na subida")]
    public void Validate_RemetenteInvalido_Falha()
    {
        ValidateOptionsResult resultado = _validator.Validate(null, ComRemetente("sem arroba"));

        resultado.Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "Remetente com nome de exibição é recusado: o nome vai em NomeRemetente")]
    public void Validate_RemetenteComNomeDeExibicao_Falha()
    {
        _validator.Validate(null, ComRemetente("Uni+ <nao-responda@unifesspa.edu.br>")).Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "Credencial sem TLS é recusada")]
    public void Validate_CredencialSemTls_Falha()
    {
        EmailSmtpOptions opcoes = Valida();
        opcoes.Seguranca = SegurancaSmtp.Nenhuma;
        opcoes.Usuario = "relay";
        opcoes.Senha = "segredo";

        _validator.Validate(null, opcoes).Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "Captura local sem credencial e sem TLS é aceita")]
    public void Validate_CapturaLocal_Passa()
    {
        EmailSmtpOptions opcoes = Valida();
        opcoes.Seguranca = SegurancaSmtp.Nenhuma;

        _validator.Validate(null, opcoes).Succeeded.Should().BeTrue();
    }

    [Theory(DisplayName = "Tempo limite de conexão fora do intervalo aceito é recusado na subida")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public void Validate_TempoLimiteDeConexaoForaDoIntervalo_Falha(int segundos)
    {
        EmailSmtpOptions opcoes = Valida();
        opcoes.TempoLimiteDeConexao = TimeSpan.FromSeconds(segundos);

        _validator.Validate(null, opcoes).Failures.Should().ContainSingle().Which.Should().Contain("TempoLimiteDeConexao");
    }

    [Theory(DisplayName = "Tempo limite de envio fora do intervalo aceito é recusado na subida")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public void Validate_TempoLimiteDeEnvioForaDoIntervalo_Falha(int segundos)
    {
        EmailSmtpOptions opcoes = Valida();
        opcoes.TempoLimiteDeEnvio = TimeSpan.FromSeconds(segundos);

        _validator.Validate(null, opcoes).Failures.Should().ContainSingle().Which.Should().Contain("TempoLimiteDeEnvio");
    }

    [Fact(DisplayName = "Sem configuração, os tempos limite têm padrão finito e válido")]
    public void Validate_TemposLimitePadrao_Passa()
    {
        EmailSmtpOptions opcoes = Valida();

        opcoes.TempoLimiteDeConexao.Should().Be(TimeSpan.FromSeconds(10));
        opcoes.TempoLimiteDeEnvio.Should().Be(TimeSpan.FromSeconds(30));
        opcoes.EnviosSimultaneos.Should().Be(5);
        _validator.Validate(null, opcoes).Succeeded.Should().BeTrue();
    }

    [Theory(DisplayName = "Teto de envios simultâneos fora do intervalo aceito é recusado na subida")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Validate_EnviosSimultaneosForaDoIntervalo_Falha(int teto)
    {
        EmailSmtpOptions opcoes = Valida();
        opcoes.EnviosSimultaneos = teto;

        _validator.Validate(null, opcoes).Failures.Should().ContainSingle().Which.Should().Contain("EnviosSimultaneos");
    }

    private static EmailSmtpOptions ComRemetente(string remetente)
    {
        EmailSmtpOptions opcoes = Valida();
        opcoes.Remetente = remetente;
        return opcoes;
    }

    private static EmailSmtpOptions Valida() => new()
    {
        Host = "relay.unifesspa.edu.br",
        Porta = 587,
        Seguranca = SegurancaSmtp.StartTls,
        Remetente = "nao-responda@unifesspa.edu.br",
    };
}
