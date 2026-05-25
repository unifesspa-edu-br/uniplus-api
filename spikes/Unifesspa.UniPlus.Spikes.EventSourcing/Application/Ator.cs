namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application;

/// <summary>
/// Ator de uma decisão em <b>claro</b> (entrada de comando, transitório em memória).
/// Nunca é persistido assim: o handler o cifra (<see cref="Portas.IProtetorPii"/>)
/// antes de anexar o evento ao stream.
/// </summary>
/// <param name="SujeitoId">Identificador estável do titular (chave do crypto-shredding).</param>
/// <param name="Nome">Nome do titular (PII).</param>
/// <param name="Cpf">CPF do titular (PII sensível).</param>
public sealed record Ator(Guid SujeitoId, string Nome, string Cpf);
