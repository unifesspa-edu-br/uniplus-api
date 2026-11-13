namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Quem pede e a quem o pedido se destina, como chegam à criação.</summary>
/// <param name="Solicitante">Quem pede, como o token o traz.</param>
/// <param name="EmailDoBeneficiario">
/// E-mail institucional do beneficiário; ausente, ou igual ao e-mail verificado do solicitante, o pedido é para si mesmo.
/// </param>
public sealed record PartesDoPedido(PessoaNoToken Solicitante, string? EmailDoBeneficiario);
