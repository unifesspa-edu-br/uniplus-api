namespace Unifesspa.UniPlus.Publicacoes.API;

using Wolverine;
using Wolverine.Postgresql;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// SPIKE #820 — routing da requisição de registro de ato: fila PostgreSQL
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
/// Uma recusa de negócio (<c>RegistroDeAtoRecusadoException</c>) não se resolve
/// tentando de novo: o tipo continuará sem versão vigente, a vaga continuará ocupada.
/// Vai direto para a dead letter, onde é visível e reprocessável depois que a causa for
/// corrigida. Já falhas transientes (deadlock, indisponibilidade momentânea) são
/// retentadas — são exatamente o caso em que insistir resolve.
/// </para>
/// </remarks>
public static class PublicacoesMessagingRegistration
{
    public const string FilaAtos = "publicacoes-atos";

    public static void ConfigurarRouting(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.PublishMessage<RegistrarAtoNormativoRequisicao>().ToPostgresqlQueue(FilaAtos);
        opts.ListenToPostgresqlQueue(FilaAtos);
    }
}
