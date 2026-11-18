namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Cria o rascunho de um processo seletivo a partir do documento da configuração, o digitado pela tela ou o importado.
/// </summary>
/// <param name="Documento">O texto JSON do documento, no formato da configuração do processo seletivo.</param>
public sealed record CriarRascunhoDeProcessoSeletivoCommand(string Documento) : ICommand<Result<RascunhoSalvoDto>>;
