namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>RecursoAcessibilidade</c> para consumo cross-módulo via
/// <see cref="IRecursoAcessibilidadeReader"/> (ADR-0056). Expõe o recurso vivo
/// (id + nome) que o Módulo Seleção lê ao montar a configuração de atendimento
/// especializado de um edital, antes de congelar por valor a identidade no edital
/// (snapshot-copy, ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Nome">Rótulo legível do recurso, chave natural (ex.: "Ledor").</param>
public sealed record RecursoAcessibilidadeView(
    Guid Id,
    string Nome);
