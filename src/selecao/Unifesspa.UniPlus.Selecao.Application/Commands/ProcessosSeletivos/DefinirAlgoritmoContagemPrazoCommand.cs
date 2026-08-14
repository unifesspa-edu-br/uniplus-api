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
    /// Falta código, versão, ou ambos — em qualquer dos três casos nada foi declarado.
    /// </summary>
    /// <remarks>
    /// A condição é <b>ou</b>, não <b>e</b>: meio par não aponta entrada nenhuma do rol de
    /// regras. Tratá-lo como declaração levaria o valor ausente ao leitor do catálogo e a
    /// recusa sairia como "não encontrado", dizendo a quem chamou que o par não existe
    /// quando o que houve foi um campo esquecido. É distinto de par completo que não
    /// resolve, esse sim com causa e remediação próprias.
    /// </remarks>
    public bool AlgoritmoNaoDeclarado =>
        string.IsNullOrWhiteSpace(Codigo) || string.IsNullOrWhiteSpace(Versao);
}
