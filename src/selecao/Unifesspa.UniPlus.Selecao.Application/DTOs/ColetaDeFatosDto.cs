namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// DTO de leitura de um fato coletado, na forma do item que a escrita aceita:
/// <see cref="Apresentacao"/> e <see cref="Regras"/>. <see cref="Opcoes"/> são as opções que o processo
/// oferece ao fato cuja fonte dos valores é o processo, ou <see langword="null"/> quando
/// os valores não vêm do processo. <see cref="Formato"/> é o formato da resposta do campo de texto
/// (livre, CPF, e-mail…), <see langword="null"/> nos demais campos.
/// </summary>
public sealed record FatoColetadoDto(
    string FatoCodigo,
    int Ordem,
    string? PaginaCodigo,
    ApresentacaoDoCampoDto Apresentacao,
    RegrasDoCampoDto Regras,
    string? Formato,
    IReadOnlyList<OpcaoDoProcessoDto>? Opcoes);

/// <summary>Uma opção que o processo oferece a um fato.</summary>
public sealed record OpcaoDoProcessoDto(string Codigo, string Rotulo, int Ordem);

/// <summary>As opções que o processo declara para um fato, na ordem declarada.</summary>
public sealed record OpcoesDeclaradasDoFatoDto(string FatoCodigo, IReadOnlyList<OpcaoDoProcessoDto> Opcoes);

/// <summary>
/// DTO de leitura de uma condição do predicado <c>quando</c> de uma regra de derivação.
/// Mesma forma da escrita (<c>CondicaoDerivacaoInput</c>).
/// </summary>
public sealed record CondicaoDerivacaoDto(string Fato, [property: AceitaCodigosDe<Operador>] string Operador, JsonElement Valor);

/// <summary>
/// DTO de leitura de uma regra de derivação. A regra incondicional tem
/// <see cref="Quando"/> <see langword="null"/> — nunca uma lista vazia. A regra do derivado
/// booleano tem <see cref="Contribui"/> <see langword="null"/>.
/// </summary>
public sealed record RegraDerivacaoDto(
    int Ordem,
    string? Contribui,
    IReadOnlyList<IReadOnlyList<CondicaoDerivacaoDto>>? Quando);

/// <summary>
/// DTO de leitura da configuração de derivação de um fato: o fato derivado e a lista
/// de regras que o resolvem.
/// </summary>
public sealed record ConfiguracaoDerivacaoDto(
    string CodigoFato,
    IReadOnlyList<RegraDerivacaoDto> Regras);
