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
