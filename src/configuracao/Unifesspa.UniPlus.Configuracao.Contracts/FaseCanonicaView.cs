namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>FaseCanonica</c> para consumo cross-módulo via
/// <see cref="IFaseCanonicaReader"/> (ADR-0056). Expõe a fase viva que o Módulo
/// Seleção lê ao montar o cronograma de um processo, antes de congelar por valor
/// (snapshot-copy, ADR-0061). O <c>DonoTipico</c> e o <c>OrigemData</c> são
/// expostos como token textual (UPPER_SNAKE).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Código canônico, chave natural da fase (ex.: "AVALIACAO").</param>
/// <param name="Nome">Rótulo de apresentação.</param>
/// <param name="Descricao">Descrição livre opcional.</param>
/// <param name="DonoTipico">Dono usual da fase (token; ex.: "CEPS") — orientativo, não vinculante.</param>
/// <param name="AgrupaEtapas">Verdadeiro apenas para a fase de avaliação (agrupa Etapas pontuadas).</param>
/// <param name="PermiteComplementacao">Se a fase admite reenvio/complementação documental.</param>
/// <param name="BaseLegal">Base legal opcional.</param>
/// <param name="ProduzResultado">Se a fase produz resultado (decide o piso mínimo do cronograma havendo vagas).</param>
/// <param name="ResultadoDefinitivo">Se o resultado produzido é definitivo (não cabe recurso).</param>
/// <param name="ColetaInscricao">Se a fase coleta inscrição (decide o piso mínimo quando a origem é inscrição própria).</param>
/// <param name="OrigemData">Quem controla a data da fase (token; "PROPRIA" ou "DELEGADA").</param>
public sealed record FaseCanonicaView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string DonoTipico,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    string? BaseLegal,
    bool ProduzResultado,
    bool ResultadoDefinitivo,
    bool ColetaInscricao,
    string OrigemData);
