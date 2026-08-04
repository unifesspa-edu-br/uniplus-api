namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Define (ou substitui) o título e o texto do termo de aceite do formulário de inscrição
/// (Story #559). Editável em rascunho (pré-publicação) e sob sessão de retificação de um
/// processo publicado, mesmo padrão dos demais <c>Definir*</c>.
/// </summary>
public sealed record DefinirFormularioCommand(
    Guid ProcessoSeletivoId,
    string? Titulo,
    string? TermoAceiteTexto,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
