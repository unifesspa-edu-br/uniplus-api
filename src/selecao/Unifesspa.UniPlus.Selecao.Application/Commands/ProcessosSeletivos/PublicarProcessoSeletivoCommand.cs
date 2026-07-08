namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Kernel.Results;

/// <summary>
/// Publica o Edital de abertura do processo (RN08, Story #759, T4 #785):
/// valida a conformidade estrutural, congela a configuração num
/// <c>SnapshotPublicacao</c> append-only e transita o status para Publicado,
/// tudo na mesma transação (CA-01/CA-02). O ator (<c>IUserContext.UserId</c>)
/// nunca é input do command — vem do contexto autenticado.
/// </summary>
public sealed record PublicarProcessoSeletivoCommand(
    Guid ProcessoSeletivoId,
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId) : ICommand<Result>;
