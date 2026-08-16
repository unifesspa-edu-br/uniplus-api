namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O código não integra o comando porque é imutável. <c>Nome</c> é <c>string?</c>,
/// não <c>string</c> (ADR-0125) — ver justificativa equivalente em
/// <see cref="CriarTipoProcessoCommand"/>.
/// </summary>
public sealed record AtualizarTipoProcessoCommand(Guid Id, string? Nome, string? Descricao = null) : ICommand<Result>;
