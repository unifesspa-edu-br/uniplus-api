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
/// Checklist de conformidade ESTRUTURAL do <c>ProcessoSeletivo</c> (CA-07, issue #1092):
/// bicondicional com os QUATRO gates estruturais que <c>Publicar</c>/<c>Retificar</c> aplicam —
/// completude dos itens obrigatórios, coerência do cronograma, cobertura da cascata de
/// remanejamento e as checagens de pré-canonicalização (exigências documentais, consequência de
/// indeferimento, referência temporal de fatos, regras de derivação e o grafo de dependência
/// conjunto). Nenhum item novo em <c>ItemConformidadeDto</c>; a leitura não altera o processo.
/// </summary>
/// <remarks>
/// <b>Não é "publicável".</b> Todos os itens <c>Ok</c> significa que o agregado está
/// estruturalmente completo e coerente — a publicação ainda pode recusar por conformidade legal
/// (<c>GET /conformidade-legal</c>), documento confirmado, tipo de ato e outras leituras
/// request-specific que só o comando de publicação avalia.
/// <para>
/// Bônus regional (0..1) e critérios de desempate (0..*) são deliberadamente opcionais na
/// modelagem da classificação e NÃO entram neste checklist — a ausência de bônus/desempate é um estado
/// válido (RN05: ausência de bônus = sem bônus), não uma pendência. Etapa também deixou de ser
/// item incondicional (Story #851 §3.5): um processo sem prova (SiSU, <c>CLASSIFICACAO-IMPORTADA</c>)
/// publica sem etapa quando o cronograma é coerente.
/// </para>
/// </remarks>
public sealed record ConformidadeProcessoSeletivoDto(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemConformidadeDto> Itens);
