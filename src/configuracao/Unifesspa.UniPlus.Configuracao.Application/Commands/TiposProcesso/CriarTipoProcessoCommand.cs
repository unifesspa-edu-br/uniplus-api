namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record CriarTipoProcessoCommand(string Codigo, string Nome, string? Descricao = null) : ICommand<Result<Guid>>;
