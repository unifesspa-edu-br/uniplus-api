namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Payload do <c>PUT /api/selecao/admin/motivos-de-decisao/{id}</c>. Só a descrição é editável:
/// tipo, código, subclassificação e sentido nascem na criação e não mudam.
/// </summary>
public sealed record AtualizarMotivoDeDecisaoCommand(Guid Id, string? Descricao) : ICommand<Result>;
