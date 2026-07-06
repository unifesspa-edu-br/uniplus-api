namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Domain.Enums;
using Kernel.Results;

/// <summary>
/// Cria a raiz do agregado <c>ProcessoSeletivo</c> em rascunho (CA-01 da
/// Story #758). Validado pelo middleware FluentValidation do Wolverine via
/// <c>CriarProcessoSeletivoCommandValidator</c>.
/// </summary>
public sealed record CriarProcessoSeletivoCommand(
    string Nome,
    TipoProcesso Tipo) : ICommand<Result<Guid>>;
