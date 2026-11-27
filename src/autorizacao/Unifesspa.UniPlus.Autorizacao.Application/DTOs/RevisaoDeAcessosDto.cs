namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Revisão periódica dos acessos na listagem, com a contagem do que falta decidir.</summary>
/// <param name="Situacao"><c>EM_ANDAMENTO</c> ou <c>CONCLUIDA</c>, derivada dos itens a cada leitura.</param>
/// <param name="Pendentes">Itens sem decisão sobre concessão ainda ativa.</param>
public sealed record RevisaoResumoDto(
    Guid Id,
    DateTimeOffset AbertaEm,
    IdentidadeDto AbertaPor,
    CodigoComRotulo<SituacaoDaRevisao> Situacao,
    int TotalDeItens,
    int Pendentes);

/// <summary>Revisão periódica dos acessos com os itens. Perfil, beneficiário e escopo são lidos da concessão.</summary>
public sealed record RevisaoDeAcessosDto(
    Guid Id,
    DateTimeOffset AbertaEm,
    IdentidadeDto AbertaPor,
    CodigoComRotulo<SituacaoDaRevisao> Situacao,
    int TotalDeItens,
    int Pendentes,
    IReadOnlyList<ItemDaRevisaoDto> Itens);

/// <summary>Item da revisão: uma concessão a confirmar ou revogar.</summary>
/// <param name="Decide">
/// Governança que decide o item: <c>REPRESENTANTE_DO_CONTROLADOR</c> ou <c>GESTOR_DE_ACESSOS</c>. No item decidido, a
/// governança com que foi decidido; no pendente, a calculada na leitura pela condição atual do beneficiário, que pode mudar
/// até a decisão.
/// </param>
/// <param name="Situacao"><c>PENDENTE</c>, <c>CONFIRMADA</c>, <c>REVOGADA</c> ou <c>CONCESSAO_ENCERRADA</c> (sem decisão, porque a concessão terminou fora da revisão).</param>
/// <param name="DecididoPor">Quem decidiu; nulo enquanto o item não foi decidido.</param>
/// <param name="EhDoLeitor">A concessão do item é de quem lê, que não pode decidi-lo.</param>
public sealed record ItemDaRevisaoDto(
    Guid Id,
    Guid ConcessaoId,
    IdentidadeDto Beneficiario,
    PerfilResumoDto Perfil,
    EscopoDto Escopo,
    DateTimeOffset? ValidaAte,
    CodigoComRotulo<TipoDeGovernanca> Decide,
    CodigoComRotulo<SituacaoDoItemDaRevisao> Situacao,
    IdentidadeDto? DecididoPor,
    DateTimeOffset? DecididoEm,
    bool EhDoLeitor);

/// <summary>Corpo do <c>POST /api/autorizacao/revisoes/{id}/itens/{item}/revogacao</c>.</summary>
/// <param name="Motivo">Obrigatório; fica na trilha da concessão.</param>
public sealed record RevogarItemDaRevisaoRequest(string? Motivo);
