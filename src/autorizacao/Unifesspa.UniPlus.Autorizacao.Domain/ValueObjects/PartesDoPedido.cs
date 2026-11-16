namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

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

    internal DomainError? RecusaDoSolicitante(IDominiosInstitucionais dominios) =>
        !ParaSiMesmo && !SolicitanteInstitucional(Solicitante, dominios)
            ? new DomainError(
                PedidoErrorCodes.SolicitanteNaoInstitucional,
                "Para pedir acesso a outra pessoa, o e-mail da sessão deve ser institucional e verificado.")
            : null;

    internal (string? Email, bool ParaSiMesmo) ResolverBeneficiario(IDominiosInstitucionais dominios, List<FieldError> erros) =>
        ParaSiMesmo
            ? (ResolverParaSiMesmo(dominios, erros), true)
            : (ResolverParaOutraPessoa(dominios, erros), false);

    private string? ResolverParaSiMesmo(IDominiosInstitucionais dominios, List<FieldError> erros)
    {
        string? doToken = Solicitante.EmailVerificadoNormalizado;
        if (doToken is null || !EmailDaPessoa.Institucional(doToken, dominios))
        {
            erros.Add(new("beneficiarioEmail", new DomainError(
                PedidoErrorCodes.BeneficiarioInvalido,
                "Para pedir para si, o e-mail da sessão deve ser institucional e verificado; para outra pessoa, informe o e-mail institucional dela.")));
        }

        if (NomeDeExibicao.Normalizar(Solicitante.Nome) is null)
        {
            erros.Add(new("beneficiario", BeneficiarioDoPedido.SemNome()));
        }

        return doToken;
    }

    private string? ResolverParaOutraPessoa(IDominiosInstitucionais dominios, List<FieldError> erros)
    {
        string? informado = EmailDaPessoa.Normalizar(EmailDoBeneficiario);
        if (informado is null || !EmailDaPessoa.Institucional(informado, dominios))
        {
            erros.Add(new("beneficiarioEmail", new DomainError(
                PedidoErrorCodes.BeneficiarioInvalido,
                "O beneficiário é identificado pelo e-mail institucional dele.")));
        }

        return informado;
    }
}
