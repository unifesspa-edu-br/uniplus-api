namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Controllers;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirTaxaInscricao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>Cobra</c> é <c>[JsonRequired]</c> (mesmo raciocínio de
/// <c>DefinirClassificacaoRequest.BaseadoEmEnem</c>, issue #850): o host não habilita
/// <c>RespectRequiredConstructorParameters</c>, então por padrão do <c>System.Text.Json</c> um
/// parâmetro de construtor ausente no JSON recebe o valor default (<see langword="null"/>) —
/// indistinguível de <c>cobra: null</c> explícito, que o handler interpreta como remoção
/// deliberada da declaração (CA-01). Omitir o campo não pode desfazer silenciosamente uma
/// configuração existente.
/// </remarks>
public sealed record DefinirTaxaInscricaoRequest(
    [property: JsonRequired] bool? Cobra,
    decimal? Valor,
    // Único lugar em que a lista permanece explícita: argumento de atributo
    // exige constante de compilação, e `FundamentoIsencaoCodigo.Codigos` é
    // calculado. Acrescentar fundamento sem incluí-lo aqui deixa o schema
    // publicado mais estreito que o vocabulário — o teste de contrato do
    // vocabulário fechado é o que impede isso passar.
    [property: VocabularioFechado(
        FundamentoIsencaoCodigo.CadastroUnico,
        FundamentoIsencaoCodigo.DoacaoMedulaOssea,
        FundamentoIsencaoCodigo.CarenciaSocioeconomica)]
    IReadOnlyList<string>? Fundamentos);
