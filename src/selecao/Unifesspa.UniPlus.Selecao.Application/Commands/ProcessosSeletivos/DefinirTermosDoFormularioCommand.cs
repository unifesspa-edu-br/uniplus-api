namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>
/// Substitui os termos exigidos pelo formulário do código informado (UNI-REQ-0086). Editável só em rascunho.
/// </summary>
public sealed record DefinirTermosDoFormularioCommand(
    Guid ProcessoSeletivoId,
    string FormularioCodigo,
    IReadOnlyList<TermoExigidoInput>? Termos) : ICommand<Result>;
