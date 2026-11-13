namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>
/// Providência sobre concessão como a API a expõe, no detalhe e na resposta dos atos. Traz o nome de exibição de quem
/// agiu, nunca o CPF nem o e-mail.
/// </summary>
/// <param name="Tipo"><c>retirada</c> ou <c>revisao</c>.</param>
/// <param name="Situacao"><c>aberta</c>, <c>respondida</c>, <c>atendida</c> ou <c>prejudicada</c>, derivada no instante da leitura.</param>
/// <param name="EncerramentoDaConcessao">O que encerrou a concessão; só na providência prejudicada.</param>
/// <param name="Desfecho">A resposta ou o atendimento; nulo enquanto aberta.</param>
/// <param name="ImpedidoDeResponder">Quem lê é quem solicitou ou o beneficiário da concessão, e por isso não responde nem atende.</param>
public sealed record ProvidenciaDto(
    Guid Id,
    string Tipo,
    string Fundamento,
    string Situacao,
    DateTimeOffset AbertaEm,
    IdentidadeDto SolicitadaPor,
    ConcessaoDaProvidenciaDto Concessao,
    EncerramentoDaConcessaoDto? EncerramentoDaConcessao,
    DesfechoDaProvidenciaDto? Desfecho,
    bool ImpedidoDeResponder);

/// <summary>
/// Item da fila das providências: o detalhe sem o fundamento e sem o motivo do desfecho, que só o detalhe traz.
/// </summary>
public sealed record ProvidenciaResumoDto(
    Guid Id,
    string Tipo,
    string Situacao,
    DateTimeOffset AbertaEm,
    IdentidadeDto SolicitadaPor,
    ConcessaoDaProvidenciaDto Concessao,
    EncerramentoDaConcessaoDto? EncerramentoDaConcessao,
    AtoDoDesfechoDto? Desfecho,
    bool ImpedidoDeResponder);

/// <summary>A concessão alvo da providência.</summary>
/// <param name="Ativa">Derivada da revogação e da vigência no momento da leitura.</param>
public sealed record ConcessaoDaProvidenciaDto(
    Guid Id, IdentidadeDto Beneficiario, PerfilResumoDto Perfil, EscopoDto Escopo, DateTimeOffset? ValidaAte, bool Ativa);

/// <summary>O que encerrou a concessão da providência prejudicada.</summary>
/// <param name="Causa"><c>revogada</c> ou <c>vencida</c>.</param>
/// <param name="Em">O instante da revogação ou o fim da vigência.</param>
public sealed record EncerramentoDaConcessaoDto(string Causa, DateTimeOffset Em);

/// <summary>A resposta ou o atendimento, com o motivo.</summary>
public sealed record DesfechoDaProvidenciaDto(DateTimeOffset Em, IdentidadeDto Por, string Motivo);

/// <summary>Quem respondeu ou atendeu, e quando, sem o motivo.</summary>
public sealed record AtoDoDesfechoDto(DateTimeOffset Em, IdentidadeDto Por);

/// <summary>Corpo do <c>POST /api/autorizacao/providencias</c>.</summary>
/// <param name="ConcessaoId">A concessão sobre a qual se pede a providência.</param>
/// <param name="Tipo">Obrigatório: <c>retirada</c> ou <c>revisao</c>.</param>
/// <param name="Fundamento">Obrigatório, de 10 a 2000 caracteres.</param>
public sealed record SolicitarProvidenciaRequest(Guid ConcessaoId, string? Tipo, string? Fundamento);

/// <summary>Corpo do <c>POST /api/autorizacao/providencias/{id}/resposta</c>.</summary>
/// <param name="Motivo">Obrigatório, até 500 caracteres.</param>
public sealed record ResponderProvidenciaRequest(string? Motivo);

/// <summary>Corpo do <c>POST /api/autorizacao/providencias/{id}/atendimento</c>, que revoga a concessão.</summary>
/// <param name="Motivo">Obrigatório, até 500 caracteres; vira o motivo da revogação na trilha da concessão.</param>
/// <param name="ConfirmacaoEmail">Obrigatório: o e-mail do beneficiário, conferido sem diferenciar maiúsculas nem espaços nas pontas.</param>
public sealed record AtenderProvidenciaRequest(string? Motivo, string? ConfirmacaoEmail);
