namespace Unifesspa.UniPlus.Publicacoes.API;

using Wolverine;
using Wolverine.Postgresql;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// routing da requisição de registro de ato: fila PostgreSQL
/// <c>publicacoes-atos</c>, o mesmo transporte durável que Seleção já usa para drenar
/// os seus domain events (<c>domain-events</c>).
/// </summary>
/// <remarks>
/// <para>
/// A fila é <b>durável</b>: o envelope é persistido no outbox, dentro da MESMA transação
/// em que o domínio grava o que publicou (ADR-0004). Ou os dois existem, ou nenhum —
/// que é a atomicidade que a orquestração síncrona não conseguia dar.
/// </para>
/// <para>
/// As políticas de falha desta mensagem vivem no próprio handler
/// (<c>RegistrarAtoNormativoRequisicaoHandler.Configure</c>), escopadas à sua chain —
/// declará-las aqui as tornaria globais, e uma política de retry global atrasaria toda
/// falha de validação de todo command HTTP do processo.
/// </para>
/// </remarks>
public static class PublicacoesMessagingRegistration
{
    public const string FilaAtos = "publicacoes-atos";

    public static void ConfigurarRouting(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        // Fila PostgreSQL, e não fila local: só a PG é DURÁVEL. A fila local do Wolverine é
        // buffered/in-memory (`IsDurable => false`) e `UseDurableOutboxOnAllSendingEndpoints`
        // exclui filas locais — um crash entre o commit de Seleção e o processamento perderia
        // a requisição, e com ela o ato. É o mesmo transporte que o `domain-events` já usa.
        opts.PublishMessage<RegistrarAtoNormativoRequisicao>().ToPostgresqlQueue(FilaAtos);
        opts.ListenToPostgresqlQueue(FilaAtos);
    }
}
