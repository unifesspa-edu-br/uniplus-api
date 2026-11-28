namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.ComponentModel;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Substitui as opções que o processo declara para um fato cuja fonte dos valores é o processo
/// (issue #1619). A ordem da lista é a ordem de exibição. A lista, o código e o rótulo de cada opção ausentes ou nulos são recusados (422) na
/// conferência das opções.
/// </summary>
public sealed record DefinirOpcoesDeclaradasCommand(
    Guid ProcessoSeletivoId,
    string FatoCodigo,
    IReadOnlyList<OpcaoDeclaradaInput>? Opcoes) : ICommand<Result>;

/// <summary>Uma opção declarada: o código que a resposta cita e o rótulo exibido.</summary>
public sealed record OpcaoDeclaradaInput(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? Codigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? Rotulo);
