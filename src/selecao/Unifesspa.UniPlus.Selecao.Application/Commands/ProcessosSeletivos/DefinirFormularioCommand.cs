namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Define ou substitui o formulário de uma finalidade (UNI-REQ-0144): a fase do cronograma, o título
/// e as etapas. Editável em rascunho e sob sessão de retificação, que pode acrescentar formulário.
/// </summary>
public sealed record DefinirFormularioCommand(
    Guid ProcessoSeletivoId,
    FinalidadeFormulario Finalidade,
    Guid? FaseId,
    string? Titulo,
    IReadOnlyList<EtapaFormularioInput> Etapas,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;

/// <summary>Remove o formulário de uma finalidade, com os itens e os termos dele. Só em rascunho.</summary>
public sealed record RemoverFormularioCommand(
    Guid ProcessoSeletivoId,
    FinalidadeFormulario Finalidade,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
