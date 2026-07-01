namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>TipoBanca</c> para consumo cross-módulo via
/// <see cref="ITipoBancaReader"/> (ADR-0056). Expõe o tipo de banca vivo que o
/// Módulo Seleção lê ao configurar as bancas requeridas por fase, antes de congelar
/// por valor (snapshot-copy, ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Código classificatório, chave natural da banca (ex.: "BANCA_ENTREVISTA").</param>
/// <param name="Nome">Rótulo de apresentação.</param>
/// <param name="FaseTipica">Fase usual da banca — rótulo de texto orientativo, não vinculante; ou null.</param>
/// <param name="Descricao">Descrição livre opcional.</param>
public sealed record TipoBancaView(
    Guid Id,
    string Codigo,
    string Nome,
    string? FaseTipica,
    string? Descricao);
