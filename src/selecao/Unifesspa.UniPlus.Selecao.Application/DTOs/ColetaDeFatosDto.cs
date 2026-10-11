namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Entradas;
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
/// Obrigatoriedade/Ajuda — Story #559, UNI-REQ-0145). A <see cref="Precondicao"/> é o predicado na forma normal
/// disjuntiva — o OU de cláusulas, cada cláusula o E de condições —, ou <see langword="null"/>
/// quando o fato é coletado incondicionalmente (nunca uma lista vazia). <see cref="Opcoes"/> são as
/// opções que o processo oferece ao fato cuja fonte dos valores é o processo (issue #1619), ou
/// <see langword="null"/> quando os valores não vêm do processo. <see cref="Formato"/> é o formato
/// da resposta do campo de texto (livre, CPF, e-mail…), <see langword="null"/> nos demais campos.
/// <see cref="Impedimento"/> é a resposta que impede a inscrição, com a mensagem ao candidato, ou
/// <see langword="null"/> quando nenhuma impede.
/// </summary>
public sealed record FatoColetadoDto(
    string FatoCodigo,
    int Ordem,
    string Rotulo,
    string TipoRenderizacao,
    ObrigatoriedadeDto Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Precondicao,
    IReadOnlyList<OpcaoDoProcessoDto>? Opcoes,
    string? EtapaCodigo,
    string? Formato,
    string? Ajuda,
    bool PedirConfirmacao,
    IReadOnlyList<RestricaoValorDto> Restricoes,
    ImpedimentoDto? Impedimento);

/// <summary>A resposta do campo que impede a inscrição e a mensagem que explica o motivo ao candidato.</summary>
public sealed record ImpedimentoDto(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> Quando, string Mensagem);

/// <summary>
/// Uma restrição sobre o valor respondido no item, pelo <see cref="Tipo"/>: <c>FAIXA_NUMERICA</c>
/// e <c>TAMANHO_TEXTO</c> com os limites, <c>OPCOES_PERMITIDAS</c> com os grupos de opções,
/// <c>OPCOES_DAS_RESPOSTAS</c> com os fatos cujas respostas formam as opções, <c>MUNICIPIOS_DA_UF</c>
/// com o fato da UF e <c>FAIXA_DE_DATA</c> com a data mínima e a máxima.
/// </summary>
public sealed record RestricaoValorDto(
    string Tipo,
    decimal? Minimo,
    decimal? Maximo,
    IReadOnlyList<OpcoesCondicionadasDto>? Entradas,
    IReadOnlyList<string>? Fatos,
    LimiteDeDataDto? DataMinima,
    LimiteDeDataDto? DataMaxima);

/// <summary>
/// Um limite da faixa de data, pela âncora: <c>DATA_FIXA</c> com a data, ou <c>ENVIO</c> — o dia do
/// envio da resposta — deslocado em anos e depois em dias, nulos no limite fixo.
/// </summary>
public sealed record LimiteDeDataDto(string Ancora, DateOnly? Data, int? Anos, int? Dias);

/// <summary>Um grupo de opções permitidas e a condição em que ele vale; nula quando vale sempre.</summary>
public sealed record OpcoesCondicionadasDto(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Quando,
    IReadOnlyList<string> Valores);

/// <summary>Uma opção que o processo oferece a um fato (issue #1619).</summary>
public sealed record OpcaoDoProcessoDto(string Codigo, string Rotulo, int Ordem);

/// <summary>As opções que o processo declara para um fato, na ordem declarada.</summary>
public sealed record OpcoesDeclaradasDoFatoDto(string FatoCodigo, IReadOnlyList<OpcaoDoProcessoDto> Opcoes);

/// <summary>
/// DTO de leitura de uma condição do predicado <c>quando</c> de uma regra de derivação (Story
/// #987). Mesma forma da escrita (<c>CondicaoDerivacaoInput</c>).
/// </summary>
public sealed record CondicaoDerivacaoDto(string Fato, string Operador, JsonElement Valor);

/// <summary>
/// DTO de leitura de uma regra de derivação (Story #987). A regra incondicional (âncora) tem
/// <see cref="Quando"/> <see langword="null"/> — nunca uma lista vazia. A regra do derivado
/// booleano tem <see cref="Contribui"/> <see langword="null"/>.
/// </summary>
public sealed record RegraDerivacaoDto(
    int Ordem,
    string? Contribui,
    IReadOnlyList<IReadOnlyList<CondicaoDerivacaoDto>>? Quando);

/// <summary>
/// DTO de leitura da configuração de derivação de um fato (Story #987): o fato derivado e a lista
/// de regras que o resolvem.
/// </summary>
public sealed record ConfiguracaoDerivacaoDto(
    string CodigoFato,
    IReadOnlyList<RegraDerivacaoDto> Regras);
