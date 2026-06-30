namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>TipoDeficiencia</c> para consumo cross-módulo via
/// <see cref="ITipoDeficienciaReader"/> (ADR-0056). Expõe o tipo vivo (id + nome)
/// que outros bounded contexts leem antes de congelar por valor a identidade
/// (snapshot-copy, ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Nome">Rótulo legível do tipo de deficiência (chave natural).</param>
public sealed record TipoDeficienciaView(
    Guid Id,
    string Nome);
