namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O código não integra o comando porque é imutável.</summary>
public sealed record AtualizarTipoEtapaCommand(Guid Id, string Nome, string? Descricao = null) : ICommand<Result>;
