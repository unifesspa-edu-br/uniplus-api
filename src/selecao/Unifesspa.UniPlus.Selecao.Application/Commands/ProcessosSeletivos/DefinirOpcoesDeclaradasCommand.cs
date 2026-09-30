namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Substitui as opções que o processo declara para um fato cuja fonte dos valores é o processo
/// (issue #1619). A ordem da lista é a ordem de exibição.
/// </summary>
public sealed record DefinirOpcoesDeclaradasCommand(
    Guid ProcessoSeletivoId,
    string FatoCodigo,
    IReadOnlyList<OpcaoDeclaradaInput> Opcoes,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;

/// <summary>Uma opção declarada: o código que a resposta cita e o rótulo exibido.</summary>
public sealed record OpcaoDeclaradaInput(string Codigo, string Rotulo);
