namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// Uma condição da regra de homologação das inscrições, como o cliente a escreve.
/// </summary>
/// <param name="Tipo">O código da condição que o sistema apura (letras maiúsculas separadas por sublinhado).</param>
/// <param name="EdicaoDoExame">A edição do exame cujo retorno do INEP a condição consulta, de 1998 a 2100.</param>
/// <param name="MotivoDesfavoravelCodigo">
/// O código do motivo que a inscrição recebe quando a condição não é cumprida. Ausente é aceito ao salvar o
/// rascunho; a cobertura dos motivos o recusa na publicação.
/// </param>
/// <param name="Enquadramento">
/// Quais candidatos a condição alcança, no formato do predicado das demais regras do formulário (o OU de cláusulas, cada
/// uma o E de condições sobre fatos do candidato). Ausente ou nulo: vale para todos. Quando informado, deve ter
/// ao menos uma cláusula.
/// </param>
public sealed record CondicaoDeHomologacaoInput(
    [property: AceitaCodigosDe<TipoDeCondicaoDeHomologacao>] string? Tipo = null,
    int? EdicaoDoExame = null,
    string? MotivoDesfavoravelCodigo = null,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Enquadramento = null);

/// <summary>
/// Um grupo de condições que valem juntas (E), na ordem em que os motivos chegam à inscrição não homologada.
/// </summary>
/// <param name="Condicoes">As condições do grupo; ausente ou vazio é recusado como alternativa sem condição.</param>
public sealed record AlternativaDeCondicoesInput(IReadOnlyList<CondicaoDeHomologacaoInput>? Condicoes = null);

/// <summary>
/// A regra com que a fase homologa as inscrições. A fase que não declara a regra não homologa: o cliente que não a
/// devolve na substituição do cronograma a remove, e a regra vazia (sem alternativa) é a que homologa toda inscrição enviada.
/// </summary>
/// <param name="ResultadoAtoCodigo">O código do tipo de ato do produto da fase que publica a lista de homologação.</param>
/// <param name="Alternativas">As alternativas (OU) de condições; ausente ou vazio é a regra que homologa toda inscrição enviada.</param>
public sealed record RegraDeHomologacaoInput(
    string? ResultadoAtoCodigo = null,
    IReadOnlyList<AlternativaDeCondicoesInput>? Alternativas = null);
