namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Os endereços, na Configuração, a que os avisos da governança de perfis levam. O link vai à tela autenticada, nunca
/// a um token.
/// </summary>
public interface ILinkDaGovernanca
{
    /// <summary>A tela do perfil.</summary>
    string DoPerfil(Guid perfilId);

    /// <summary>A tela da versão do perfil.</summary>
    string DaVersao(Guid perfilId, Guid versaoId);
}
