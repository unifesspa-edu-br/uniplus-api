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

    /// <summary>A tela da janela de migração do perfil.</summary>
    string DaJanela(Guid perfilId);

    /// <summary>
    /// A tela dos pedidos da pessoa, aberta a quem entrou no Uni+. Os avisos ao beneficiário apontam para ela porque a
    /// tela das concessões exige administrá-las; fica até existir a tela dos acessos da pessoa (uniplus-web#1457).
    /// </summary>
    string DosPedidos();

    /// <summary>A tela para pedir um acesso.</summary>
    string DoNovoPedido();

    /// <summary>A tela da providência sobre uma concessão.</summary>
    string DaProvidencia(Guid providenciaId);
}
