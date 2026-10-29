namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Os domínios de e-mail institucional: o código de confirmação da instalação só é entregue a
/// e-mail deles, e o beneficiário de um pedido de concessão é identificado por e-mail deles. O e-mail
/// do token que vem do gov.br pode ser pessoal.
/// </summary>
public interface IDominiosInstitucionais
{
    bool Contem(string dominio);
}
