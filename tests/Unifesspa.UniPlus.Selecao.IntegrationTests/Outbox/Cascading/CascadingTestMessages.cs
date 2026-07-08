namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Comando de teste exclusivo do cenário V9 (rollback cascading) — publica um
/// processo recém-semeado e força uma exceção logo após o
/// <c>SaveChanges</c>, simulando falha pós-persistência. Reusa a orquestração
/// real de <c>ProcessoSeletivo.Publicar</c> (não um agregado de brinquedo).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Mensagens descobertas pela convenção do Wolverine precisam ser públicas.")]
public sealed record FalharAposPublicarCascadingCommand(string NomeProcesso);
