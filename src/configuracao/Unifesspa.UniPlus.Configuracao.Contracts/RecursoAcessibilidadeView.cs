namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>RecursoAcessibilidade</c> para consumo cross-módulo via
/// <see cref="IRecursoAcessibilidadeReader"/> (ADR-0056). Expõe o recurso vivo
/// (id + código + nome) que o Módulo Seleção lê ao montar a configuração de atendimento
/// especializado de um edital, antes de congelar por valor a identidade no edital
/// (snapshot-copy, ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Identidade semântica do recurso (UNI-REQ-0061), congelada na oferta e escolhida pelo candidato (ex.: <c>LEDOR</c>).</param>
/// <param name="Nome">Rótulo legível do recurso (ex.: "Ledor").</param>
public sealed record RecursoAcessibilidadeView(
    Guid Id,
    string Codigo,
    string Nome);
