namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Os domínios de e-mail institucional em que o código de confirmação da instalação pode ser entregue. O e-mail do
/// token que vem do gov.br pode ser pessoal, e o código só vai a e-mail institucional.
/// </summary>
public interface IDominiosInstitucionais
{
    bool Contem(string dominio);
}
