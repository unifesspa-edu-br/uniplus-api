namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Um fundamento de isenção que um processo com taxa pode referenciar (UNI-REQ-0101).
/// Existe para que o cliente descubra os códigos em runtime, em vez de manter uma cópia
/// deles — uma cópia envelhece sem avisar, e o drift só aparece como requisição recusada.
/// </summary>
public sealed record FundamentoIsencaoDto(
    string Codigo,
    string Nome,
    string Descricao);

/// <summary>
/// Um campo permitido na divulgação pública de resultado (UNI-REQ-0050).
/// </summary>
/// <param name="Obrigatorio">
/// Piso da divulgação: está sempre presente e nenhuma configuração o remove. Quem monta a
/// tela usa isto para desabilitar a exclusão, em vez de descobrir a regra pela recusa.
/// </param>
/// <param name="ExigeJustificativa">Publicar este campo obriga a declarar justificativa.</param>
public sealed record CampoDivulgacaoDto(
    string Codigo,
    string Nome,
    bool Obrigatorio,
    bool ExigeJustificativa);
