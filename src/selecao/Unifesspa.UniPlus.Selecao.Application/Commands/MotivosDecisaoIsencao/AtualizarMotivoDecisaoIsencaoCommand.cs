namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Payload do <c>PUT /api/selecao/admin/motivos-decisao-isencao/{id}</c>. Só a
/// descrição é editável — código, fundamento e resultado permitido são
/// definidos na criação e não mudam mais (UNI-REQ-0121).
/// </summary>
public sealed record AtualizarMotivoDecisaoIsencaoCommand(
    Guid Id,
    string? Descricao) : ICommand<Result>;
