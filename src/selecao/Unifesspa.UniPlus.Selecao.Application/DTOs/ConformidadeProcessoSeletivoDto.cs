namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Veredicto de um item do checklist de conformidade ESTRUTURAL do <c>ProcessoSeletivo</c>
/// (Story #758, CA-07, issue #1092).
/// </summary>
/// <remarks>
/// A identidade do item é o par <paramref name="Codigo"/>/<paramref name="Dimensao"/>, e não
/// a mensagem: um cliente que precise levar quem publica até a seção correta do editor liga a
/// navegação ao código, não ao texto. A mensagem é redação, e redação melhora — uma tabela de
/// frases no cliente quebraria na primeira revisão editorial, e quebraria em silêncio.
/// </remarks>
/// <param name="Codigo">Identificador estável da invariante, único no checklist.</param>
/// <param name="Dimensao">Seção do agregado onde a pendência se corrige.</param>
/// <param name="Mensagem">Texto humano do item; pode mudar sem que código ou dimensão mudem.</param>
/// <param name="Ok">Veredicto do item no estado atual do agregado.</param>
public sealed record ItemConformidadeDto(string Codigo, string Dimensao, string Mensagem, bool Ok);

/// <summary>
/// Aviso da conformidade do <c>ProcessoSeletivo</c>: algo que o administrador deve confirmar e
/// que nunca impede a publicação. Não é item do checklist e não entra na bicondicional com os
/// gates.
/// </summary>
/// <remarks>
/// Identidade pelo par <paramref name="Codigo"/>/<paramref name="Dimensao"/>, como no item.
/// </remarks>
/// <param name="Codigo">Identificador estável do aviso, de um conjunto fechado.</param>
/// <param name="Dimensao">Seção do agregado onde o administrador confere.</param>
/// <param name="Mensagem">Texto humano do aviso, com o lugar do cronograma; pode mudar sem que código ou dimensão mudem.</param>
public sealed record AvisoConformidadeDto(string Codigo, string Dimensao, string Mensagem);

/// <summary>
/// Checklist de conformidade ESTRUTURAL do <c>ProcessoSeletivo</c> (CA-07, issue #1092):
/// bicondicional com os SEIS gates estruturais que <c>Publicar</c>/<c>Retificar</c> aplicam —
/// localidade que rege a contagem dos prazos, convenção de contagem declarada, completude dos
/// itens obrigatórios, coerência do cronograma, cobertura da cascata de remanejamento e as
/// checagens de pré-canonicalização (exigências documentais, consequência de indeferimento,
/// referência temporal de fatos, regras de derivação e o grafo de dependência conjunto). A leitura não altera o processo.
/// </summary>
/// <remarks>
/// <b>Não é "publicável".</b> Todos os itens <c>Ok</c> significa que o agregado está
/// estruturalmente completo e coerente — a publicação ainda pode recusar por conformidade legal
/// (<c>GET /conformidade-legal</c>), documento confirmado, tipo de ato e outras leituras
/// request-specific que só o comando de publicação avalia.
/// <para>
/// O bônus regional e os critérios de desempate entram neste checklist: o processo declara se
/// aplica o bônus (a ausência da declaração é pendência; a da configuração só significa "sem
/// bônus" quando a declaração é falsa), e o desempate é obrigatório quando a inscrição é feita no
/// sistema (com resultado importado a lista já vem classificada). Etapa também deixou de ser
/// item incondicional (Story #851 §3.5): um processo sem prova (SiSU, <c>CLASSIFICACAO-IMPORTADA</c>)
/// publica sem etapa quando o cronograma é coerente.
/// </para>
/// <para>
/// <b>Avisos não bloqueiam.</b> <c>Avisos</c> é uma lista própria, fora de <c>Itens</c>: o que o
/// administrador deve confirmar sem que a publicação recuse. Um cliente libera o botão de
/// publicar pelos itens e mostra os avisos à parte; a lista vazia é o estado normal.
/// </para>
/// </remarks>
/// <param name="ProcessoSeletivoId">Processo avaliado.</param>
/// <param name="Itens">Checklist estrutural, bicondicional com os gates.</param>
/// <param name="Avisos">O que confirmar antes de publicar; nunca impede a publicação.</param>
public sealed record ConformidadeProcessoSeletivoDto(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemConformidadeDto> Itens,
    IReadOnlyList<AvisoConformidadeDto> Avisos);
