namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Veredicto de um item obrigatório do checklist de conformidade do
/// <c>ProcessoSeletivo</c> (Story #758, CA-07).
/// </summary>
public sealed record ItemConformidadeDto(string Item, bool Ok);

/// <summary>
/// Checklist de conformidade estrutural do <c>ProcessoSeletivo</c> (CA-07):
/// cada dimensão estruturalmente OBRIGATÓRIA marcada ok/pendente, sem alterar
/// o processo. Cobre Etapas (1..*), Oferta de atendimento especializado (1),
/// Distribuição de vagas (1..*) e Classificação (1 — 15º bloco canônico).
/// Bônus regional (0..1) e critérios de desempate (0..*) são deliberadamente
/// opcionais na modelagem (P-B) e NÃO entram neste checklist — a ausência de
/// bônus/desempate é um estado válido (RN05: ausência de bônus = sem bônus),
/// não uma pendência.
/// </summary>
public sealed record ConformidadeProcessoSeletivoDto(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemConformidadeDto> Itens);
