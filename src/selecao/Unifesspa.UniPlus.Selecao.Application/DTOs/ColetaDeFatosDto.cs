namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// DTO de leitura de uma condição de pré-condição de fato coletado (Story #987). Mesma forma flat
/// do wire de escrita (<c>CondicaoPrecondicaoInput</c>): fecha o round-trip GET→PUT sem
/// transformação do cliente. O <see cref="Valor"/> é o valor JSON já tipado (string, booleano,
/// número ou array), não um texto — o mesmo que a escrita recebe.
/// </summary>
public sealed record CondicaoPrecondicaoDto(string Fato, string Operador, JsonElement Valor);

/// <summary>
/// DTO de leitura de um fato coletado (Story #987; apresentação — Rotulo/TipoRenderizacao/
/// Obrigatorio — Story #559). A <see cref="Precondicao"/> é o predicado na forma normal
/// disjuntiva — o OU de cláusulas, cada cláusula o E de condições —, ou <see langword="null"/>
/// quando o fato é coletado incondicionalmente (nunca uma lista vazia). <see cref="Opcoes"/> são as
/// opções que o processo oferece ao fato cuja fonte dos valores é o processo (issue #1619), ou
/// <see langword="null"/> quando os valores não vêm do processo.
/// </summary>
public sealed record FatoColetadoDto(
    string FatoCodigo,
    int Ordem,
    string Rotulo,
    string TipoRenderizacao,
    bool Obrigatorio,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Precondicao,
    IReadOnlyList<OpcaoDoProcessoDto>? Opcoes,
    string? EtapaCodigo);

/// <summary>Uma opção que o processo oferece a um fato (issue #1619).</summary>
public sealed record OpcaoDoProcessoDto(string Codigo, string Rotulo, int Ordem);

/// <summary>
/// DTO de leitura de uma condição do predicado <c>quando</c> de uma regra de derivação (Story
/// #987). Mesma forma da escrita (<c>CondicaoDerivacaoInput</c>).
/// </summary>
public sealed record CondicaoDerivacaoDto(string Fato, string Operador, JsonElement Valor);

/// <summary>
/// DTO de leitura de uma regra de derivação (Story #987). A regra incondicional (âncora) tem
/// <see cref="Quando"/> <see langword="null"/> — nunca uma lista vazia.
/// </summary>
public sealed record RegraDerivacaoDto(
    int Ordem,
    string Contribui,
    IReadOnlyList<IReadOnlyList<CondicaoDerivacaoDto>>? Quando);

/// <summary>
/// DTO de leitura da configuração de derivação de um fato (Story #987): o fato derivado e a lista
/// de regras que o resolvem.
/// </summary>
public sealed record ConfiguracaoDerivacaoDto(
    string CodigoFato,
    IReadOnlyList<RegraDerivacaoDto> Regras);
