namespace Unifesspa.UniPlus.Spikes.EventSourcing.Domain;

/// <summary>
/// PII do ator de uma decisão (quem abriu/publicou/retificou) carregada no evento
/// já <b>cifrada</b>, nunca em claro. O log append-only só guarda o ciphertext e o
/// identificador do titular (<see cref="SujeitoId"/>); a chave vive num repositório
/// separado. "Esquecer" o titular = apagar a chave (crypto-shredding): o fato
/// permanece no stream, mas o conteúdo torna-se indecifrável (gate G3 / LGPD).
/// </summary>
/// <param name="SujeitoId">Identificador estável do titular dos dados pessoais.</param>
/// <param name="ChaveId">
/// Identidade da chave <b>exata</b> que cifrou este conteúdo. Cada evento referencia
/// a sua própria chave: a leitura decifra com ela e nunca com uma chave substituta,
/// então esquecer o titular (apagar suas chaves) torna o conteúdo irrecuperável sem
/// risco de decifrar com chave errada — e o reaparecimento do titular gera uma chave
/// nova sem corromper os eventos antigos.
/// </param>
/// <param name="Conteudo">
/// Base64 de <c>nonce(12) | ciphertext | tag(16)</c> produzido por AES-256-GCM.
/// </param>
public sealed record AtorCifrado(Guid SujeitoId, Guid ChaveId, string Conteudo);
