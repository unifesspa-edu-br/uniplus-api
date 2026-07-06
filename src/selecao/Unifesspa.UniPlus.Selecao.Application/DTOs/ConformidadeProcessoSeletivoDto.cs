namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Veredicto de um item obrigatório do checklist de conformidade do
/// <c>ProcessoSeletivo</c> (Story #758, CA-07).
/// </summary>
public sealed record ItemConformidadeDto(string Item, bool Ok);

/// <summary>
/// Checklist de conformidade estrutural do <c>ProcessoSeletivo</c> (CA-07):
/// cada dimensão estruturalmente obrigatória marcada ok/pendente, sem alterar
/// o processo. Nesta fatia (fundação) o checklist cobre as dimensões já
/// modeladas no agregado: Etapas (1..*) e Oferta de atendimento
/// especializado (1). As dimensões que dependem do catálogo de regras
/// tipadas versionadas (distribuição de vagas, bônus, critérios de desempate
/// e classificação) passam a integrar o checklist nas fatias seguintes,
/// quando existirem no agregado — o conjunto retornado reflete apenas as
/// dimensões disponíveis nesta versão.
/// </summary>
public sealed record ConformidadeProcessoSeletivoDto(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ItemConformidadeDto> Itens);
