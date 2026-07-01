namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>Modalidade</c> de concorrência para consumo cross-módulo via
/// <see cref="IModalidadeReader"/> (ADR-0056). Expõe a modalidade viva que o Módulo
/// Seleção lê ao montar as modalidades de um edital, antes de congelar por valor a
/// identidade (snapshot-copy, ADR-0061). Os enums são expostos como tokens textuais
/// (UPPER_SNAKE).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Código classificatório, chave natural da modalidade (ex.: "LB_PPI").</param>
/// <param name="Descricao">Descrição livre opcional.</param>
/// <param name="NaturezaLegal">Natureza jurídica (token; ex.: "COTA_RESERVADA").</param>
/// <param name="ComposicaoVagas">Forma de composição das vagas (token; ex.: "RETIRA_DE").</param>
/// <param name="ComposicaoOrigem">Código da modalidade de origem (só em RETIRA_DE), ou null.</param>
/// <param name="RegraRemanejamento">Regra de remanejamento (token) ou null.</param>
/// <param name="RemanejamentoDestino">Código de destino único (regra DESTINO_UNICO) ou null.</param>
/// <param name="RemanejamentoPar">Código par (regra CRUZADO) ou null.</param>
/// <param name="RemanejamentoFallback">Código de fallback (regra CRUZADO) ou null.</param>
/// <param name="CriteriosCumulativos">Critérios cumulativos declarados (pode ser vazio).</param>
/// <param name="AcaoQuandoIndeferido">Ação ao indeferir (token) ou null.</param>
/// <param name="BaseLegal">Base legal opcional.</param>
public sealed record ModalidadeView(
    Guid Id,
    string Codigo,
    string? Descricao,
    string NaturezaLegal,
    string ComposicaoVagas,
    string? ComposicaoOrigem,
    string? RegraRemanejamento,
    string? RemanejamentoDestino,
    string? RemanejamentoPar,
    string? RemanejamentoFallback,
    IReadOnlyList<string> CriteriosCumulativos,
    string? AcaoQuandoIndeferido,
    string? BaseLegal);
