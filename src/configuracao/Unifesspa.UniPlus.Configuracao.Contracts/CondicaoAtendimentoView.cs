namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>CondicaoAtendimentoEspecializado</c> para consumo
/// cross-módulo via <see cref="ICondicaoAtendimentoReader"/> (ADR-0056). Expõe a
/// condição viva (código + nome) que o Módulo Seleção lê ao montar as opções de
/// atendimento especializado de um edital, antes de congelar por valor a
/// identidade na solicitação (snapshot-copy, ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Código classificatório, chave natural da condição (ex.: "PCD").</param>
/// <param name="Nome">Rótulo legível da condição.</param>
public sealed record CondicaoAtendimentoView(
    Guid Id,
    string Codigo,
    string Nome);
