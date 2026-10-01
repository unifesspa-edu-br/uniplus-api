namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Substitui os termos exigidos pelo formulário de inscrição (UNI-REQ-0086). Editável em rascunho
/// e sob sessão de retificação, com o mesmo padrão dos demais <c>Definir*</c>.
/// </summary>
public sealed record DefinirTermosDoFormularioCommand(
    Guid ProcessoSeletivoId,
    FinalidadeFormulario Finalidade,
    IReadOnlyList<TermoExigidoInput> Termos,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
