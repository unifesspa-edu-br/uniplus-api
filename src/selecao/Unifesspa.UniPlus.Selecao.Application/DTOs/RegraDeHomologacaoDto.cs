namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// Projeção de leitura de uma condição da regra de homologação das inscrições, na mesma forma da escrita: o cliente devolve o que leu.
/// </summary>
/// <param name="Tipo">O código da condição que o sistema apura.</param>
/// <param name="EdicaoDoExame">A edição do exame cujo retorno do INEP a condição consulta.</param>
/// <param name="MotivoDesfavoravelCodigo">O código do motivo que a inscrição recebe quando a condição não é cumprida, ou nulo enquanto o rascunho não o escolhe.</param>
/// <param name="Enquadramento">Quais candidatos a condição alcança, como o OU de cláusulas (cada uma o E de condições), ou nulo quando vale para todos.</param>
public sealed record CondicaoDeHomologacaoDto(
    string Tipo,
    int EdicaoDoExame,
    string? MotivoDesfavoravelCodigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Enquadramento);

/// <summary>Projeção de leitura de um grupo de condições que valem juntas (E), na ordem declarada.</summary>
public sealed record AlternativaDeCondicoesDto(IReadOnlyList<CondicaoDeHomologacaoDto> Condicoes);

/// <summary>Projeção de leitura da regra com que a fase homologa as inscrições.</summary>
/// <param name="ResultadoAtoCodigo">O código do tipo de ato do produto da fase que publica a lista de homologação, ou nulo enquanto o rascunho não o aponta.</param>
/// <param name="Alternativas">As alternativas (OU) de condições, na ordem declarada; vazio quando a fase homologa toda inscrição enviada.</param>
public sealed record RegraDeHomologacaoDto(string? ResultadoAtoCodigo, IReadOnlyList<AlternativaDeCondicoesDto> Alternativas);
