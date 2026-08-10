namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O código não integra o comando porque é imutável.</summary>
public sealed record AtualizarTipoProcessoCommand(Guid Id, string Nome, string? Descricao = null) : ICommand<Result>;
