namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirReferenciaTemporalFatos"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// Contrato por variante de <c>Tipo</c> (Story #554, PR #896 — issue #892; tokens em
/// <see cref="Domain.Enums.ReferenciaTipoCodigo"/>), tudo-ou-nada por linha (N-I01):
/// <list type="table">
/// <listheader><term>Tipo</term><description>Data</description><description>FaseId</description></listheader>
/// <item><term><see langword="null"/> (remove a referência)</term><description>proibido</description><description>proibido</description></item>
/// <item><term><c>FIM_INSCRICAO</c></term><description>proibido</description><description>proibido</description></item>
/// <item><term><c>INICIO_FASE</c> / <c>FIM_FASE</c></term><description>proibido</description><description>obrigatório</description></item>
/// <item><term><c>DATA_ESPECIFICA</c></term><description>obrigatório</description><description>proibido</description></item>
/// </list>
/// Violação de qualquer linha recusa com <c>ReferenciaTemporalFatos.DataIncoerenteComTipo</c>
/// ou <c>ReferenciaTemporalFatos.FaseIncoerenteComTipo</c> (422) — sem fallback silencioso
/// (ADR-0111:235-236). <c>FaseId</c>, quando presente, precisa pertencer ao cronograma do
/// MESMO processo (<c>ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso</c>).
/// </remarks>
public sealed record DefinirReferenciaTemporalFatosRequest(
    string? Tipo,
    DateOnly? Data,
    Guid? FaseId);
