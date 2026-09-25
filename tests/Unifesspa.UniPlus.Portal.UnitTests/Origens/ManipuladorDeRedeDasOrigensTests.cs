namespace Unifesspa.UniPlus.Portal.UnitTests.Origens;

using AwesomeAssertions;

using Unifesspa.UniPlus.Portal.Infrastructure.Origens;

/// <summary>
/// Confere o manipulador de rede das origens diretamente: nos testes da cadeia de envio ele é
/// justamente a peça trocada pela rede simulada.
/// </summary>
public sealed class ManipuladorDeRedeDasOrigensTests
{
    [Fact]
    public void Nao_guarda_cookie_nem_segue_redirecionamento_nem_usa_credencial_do_processo()
    {
        using HttpClientHandler manipulador = OrigensRegistration.SemCredencialNemRedirecionamento();

        manipulador.UseCookies.Should().BeFalse(
            "um cookie devolvido por uma origem seria reenviado em leituras de outros candidatos");
        manipulador.AllowAutoRedirect.Should().BeFalse();
        manipulador.UseDefaultCredentials.Should().BeFalse();
    }
}
