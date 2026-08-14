namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara a convenção de contagem que o certame usa nos prazos que distinguem dia útil
/// (UNI-REQ-0112). Quem configura informa <b>código e versão</b>; o servidor resolve a
/// entrada no rol de regras e congela a identidade completa, incluindo o hash.
/// </summary>
/// <remarks>
/// <para>
/// O hash não é aceito do cliente de propósito: ecoado do payload, não provaria nada sobre
/// a definição efetivamente aplicada — provaria apenas que quem chamou sabia repeti-lo.
/// </para>
/// <para>
/// Exige precondição <c>If-Match</c> porque a declaração é admitida sob sessão editorial
/// aberta, onde o ETag da sessão é o que impede escrita concorrente sobre revisão velha.
/// </para>
/// </remarks>
public sealed record DefinirAlgoritmoContagemPrazoCommand(
    Guid ProcessoSeletivoId,
    string? Codigo,
    string? Versao,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>
{
    /// <summary>
    /// Nenhum dos dois campos foi informado — distinto de par informado que não resolve no
    /// rol de regras, que tem causa e remediação próprias.
    /// </summary>
    public bool AlgoritmoNaoDeclarado =>
        string.IsNullOrWhiteSpace(Codigo) && string.IsNullOrWhiteSpace(Versao);
}
