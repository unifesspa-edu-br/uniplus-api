namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Cria a raiz do agregado <c>ProcessoSeletivo</c> em rascunho (CA-01 da
/// Story #758). Validado pelo middleware FluentValidation do Wolverine via
/// <c>CriarProcessoSeletivoCommandValidator</c>.
/// </summary>
/// <param name="OrigemCandidatos">De onde vêm os candidatos (Story #851 §3.4) — NOT NULL, exigido na criação.</param>
public sealed record CriarProcessoSeletivoCommand(
    string Nome,
    TipoProcesso Tipo,
    OrigemCandidatos OrigemCandidatos) : ICommand<Result<Guid>>;
