namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Quem pede e a quem o pedido se destina, como chegam à criação.</summary>
/// <param name="Solicitante">Quem pede, como o token o traz.</param>
/// <param name="EmailDoBeneficiario">
/// E-mail institucional do beneficiário; ausente, ou igual ao e-mail verificado do solicitante, o pedido é para si mesmo.
/// </param>
public sealed record PartesDoPedido(PessoaNoToken Solicitante, string? EmailDoBeneficiario)
{
    /// <summary>
    /// O beneficiário é o próprio solicitante quando o e-mail vem em branco ou é o do token dele, verificado: quem usa o
    /// próprio e-mail já provou que é ele, e não há ninguém a quem esperar.
    /// </summary>
    public bool ParaSiMesmo =>
        string.IsNullOrWhiteSpace(EmailDoBeneficiario)
        || (EmailDaPessoa.Normalizar(EmailDoBeneficiario) is { } informado && informado == Solicitante.EmailVerificadoNormalizado);

    /// <summary>O e-mail da sessão é verificado e de domínio institucional: quem pede por outra pessoa responde por ela.</summary>
    public static bool SolicitanteInstitucional(PessoaNoToken pessoa, IDominiosInstitucionais dominios)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return pessoa.EmailVerificadoNormalizado is { } email && EmailDaPessoa.Institucional(email, dominios);
    }
}
