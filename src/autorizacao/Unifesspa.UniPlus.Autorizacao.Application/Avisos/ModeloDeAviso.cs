namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>
/// O conteúdo de um aviso da governança de perfis: só campos estruturados e não pessoais. Não existe campo de texto
/// livre: motivo, descrição, nome de pessoa e e-mail não têm onde entrar.
/// </summary>
/// <param name="Tipo">O que o aviso comunica.</param>
/// <param name="PerfilNome">O nome do perfil, do cadastro.</param>
/// <param name="VersaoNumero">O número da versão a que o aviso se refere, quando há.</param>
/// <param name="Permissoes">Os nomes das permissões a que o aviso se refere, como o catálogo os dá.</param>
/// <param name="Link">O endereço da tela autenticada em que a pessoa age.</param>
/// <param name="Quantidade">A quantidade de concessões a que o aviso se refere, quando há.</param>
/// <param name="Prazo">O fim da janela de migração, quando o aviso é dela.</param>
public sealed record ModeloDeAviso(
    TipoDeAviso Tipo,
    string PerfilNome,
    int? VersaoNumero,
    IReadOnlyList<string> Permissoes,
    string Link,
    int? Quantidade = null,
    DateTimeOffset? Prazo = null);
