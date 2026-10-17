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

/// <summary>
/// Comando de teste do registro do ato do processo: faz o que o comando de publicação fará — grava
/// o registro e entrega a requisição de registro do ato ao outbox, na mesma transação. Com
/// <see cref="FalharDepoisDeGravar"/>, lança logo depois do <c>SaveChanges</c>, para provar que o
/// registro e o envio andam juntos.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Mensagens descobertas pela convenção do Wolverine precisam ser públicas.")]
public sealed record RegistrarAtoDoProcessoDeTesteCommand(
    Guid AtoId,
    Guid ProcessoSeletivoId,
    Guid DocumentoPrincipalId,
    bool FalharDepoisDeGravar);
