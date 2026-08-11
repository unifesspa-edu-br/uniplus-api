namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record CriarTipoEtapaCommand(string Codigo, string Nome, string? Descricao = null) : ICommand<Result<Guid>>;
