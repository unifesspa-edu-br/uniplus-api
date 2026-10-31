namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Revisão periódica dos acessos na listagem, com a contagem do que falta decidir.</summary>
/// <param name="Situacao"><c>em_andamento</c> ou <c>concluida</c>, derivada dos itens a cada leitura.</param>
/// <param name="Pendentes">Itens sem decisão sobre concessão ainda ativa.</param>
public sealed record RevisaoResumoDto(
    Guid Id,
    DateTimeOffset AbertaEm,
    IdentidadeDto AbertaPor,
    string Situacao,
    int TotalDeItens,
    int Pendentes);

/// <summary>Revisão periódica dos acessos com os itens. Perfil, beneficiário e escopo são lidos da concessão.</summary>
public sealed record RevisaoDeAcessosDto(
    Guid Id,
    DateTimeOffset AbertaEm,
    IdentidadeDto AbertaPor,
    string Situacao,
    int TotalDeItens,
    int Pendentes,
    IReadOnlyList<ItemDaRevisaoDto> Itens);

/// <summary>Item da revisão: uma concessão a confirmar ou revogar.</summary>
/// <param name="Decide">Governança que decide o item: <c>protecao-de-dados</c> ou <c>gestor-de-acessos</c>.</param>
/// <param name="Situacao"><c>pendente</c>, <c>confirmada</c>, <c>revogada</c> ou <c>concessao_encerrada</c> (sem decisão, porque a concessão terminou fora da revisão).</param>
/// <param name="DecididoPor">Quem decidiu; nulo enquanto o item não foi decidido.</param>
public sealed record ItemDaRevisaoDto(
    Guid Id,
    Guid ConcessaoId,
    IdentidadeDto Beneficiario,
    PerfilResumoDto Perfil,
    EscopoDto Escopo,
    DateTimeOffset? ValidaAte,
    string Decide,
    string Situacao,
    IdentidadeDto? DecididoPor,
    DateTimeOffset? DecididoEm);

/// <summary>Corpo do <c>POST /api/autorizacao/revisoes/{id}/itens/{item}/revogacao</c>.</summary>
/// <param name="Motivo">Obrigatório; fica na trilha da concessão.</param>
public sealed record RevogarItemDaRevisaoRequest(string? Motivo);
