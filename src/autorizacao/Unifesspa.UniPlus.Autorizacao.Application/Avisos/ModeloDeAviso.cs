namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Quantidades de concessões a que um aviso se refere; cada uma só existe nos avisos que a usam.</summary>
/// <param name="Pendentes">As que ainda esperam decisão.</param>
/// <param name="Aguardando">As que passaram a aguardar a proteção de dados.</param>
/// <param name="Migradas">As que migraram.</param>
/// <param name="Revogadas">As que perderam o acesso.</param>
public sealed record ContagensDoAviso(int? Pendentes = null, int? Aguardando = null, int? Migradas = null, int? Revogadas = null);

/// <summary>
/// O conteúdo de um aviso da governança de perfis: só campos estruturados e não pessoais. Não existe campo de texto
/// livre: motivo, descrição, nome de pessoa e e-mail não têm onde entrar.
/// </summary>
/// <param name="Tipo">O que o aviso comunica.</param>
/// <param name="PerfilNome">O nome do perfil, do cadastro.</param>
/// <param name="VersaoNumero">O número da versão a que o aviso se refere, quando há.</param>
/// <param name="Permissoes">Os nomes das permissões a que o aviso se refere, como o catálogo os dá.</param>
/// <param name="Link">O endereço da tela autenticada em que a pessoa age.</param>
/// <param name="Contagens">As quantidades de concessões, quando o aviso as traz.</param>
/// <param name="Prazo">O fim da janela de migração ou o vencimento da versão, quando o aviso é de um prazo.</param>
/// <param name="Mudanca">O que mudou na permissão, em texto fixo do vocabulário canônico, quando o aviso é da reclassificação.</param>
/// <param name="Etapa">A etapa de aprovação a que o aviso se refere, quando há.</param>
/// <param name="Forma">Como o fato aconteceu, quando o texto depende disso.</param>
/// <param name="HorasRestantes">Quantas horas faltam para o prazo, quando o aviso as anuncia.</param>
public sealed record ModeloDeAviso(
    TipoDeAviso Tipo,
    string PerfilNome,
    int? VersaoNumero,
    IReadOnlyList<string> Permissoes,
    string Link,
    ContagensDoAviso? Contagens = null,
    DateTimeOffset? Prazo = null,
    string? Mudanca = null,
    EtapaDaVersao? Etapa = null,
    FormaDoAviso? Forma = null,
    int? HorasRestantes = null);
