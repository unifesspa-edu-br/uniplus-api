namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma modalidade de concorrência (UNI-REQ-0011): código (chave natural
/// imutável), descrição opcional, e os campos de cota como tokens canônicos
/// UPPER_SNAKE para os enums (<c>NaturezaLegal</c>, <c>ComposicaoVagas</c>,
/// <c>RegraRemanejamento</c>, <c>AcaoQuandoIndeferido</c>). Quando ausentes,
/// <c>NaturezaLegal</c> assume AMPLA e <c>ComposicaoVagas</c> assume RESIDUAL_DO_VO.
/// O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarModalidadeCommand(
    string Codigo,
    string? Descricao = null,
    string? NaturezaLegal = null,
    string? ComposicaoVagas = null,
    string? ComposicaoOrigem = null,
    string? RegraRemanejamento = null,
    string? RemanejamentoDestino = null,
    string? RemanejamentoPar = null,
    string? RemanejamentoFallback = null,
    IReadOnlyList<string>? CriteriosCumulativos = null,
    string? AcaoQuandoIndeferido = null,
    string? BaseLegal = null) : ICommand<Result<Guid>>;
