namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json;

/// <summary>
/// Representação de manutenção de um fato do catálogo (ADR-0136): os eixos, a proteção de dados e
/// os valores de domínio, com o estado de cada um, e as regras padrão do derivado por regra. Os enums
/// saem como token canônico UPPER_SNAKE.
/// </summary>
public sealed record FatoCandidatoDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Dominio,
    string Origem,
    string Cardinalidade,
    string? FonteValores,
    string? Formato,
    string PontoResolucao,
    string Binding,
    string Escopo,
    string ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal,
    bool Sistema,
    bool Ativo,
    IReadOnlyList<FatoValorDominioDto> Valores,
    IReadOnlyList<RegraPadraoDto> RegrasPadrao);

/// <summary>Um valor de domínio de um fato de fonte global.</summary>
public sealed record FatoValorDominioDto(string Codigo, string? Descricao, int Ordem, bool Ativo);

/// <summary>
/// Uma regra padrão do derivado: o que contribui (nulo no booleano) e o predicado, OU de cláusulas
/// que são E de condições; vazio na regra incondicional.
/// </summary>
public sealed record RegraPadraoDto(string? Contribui, IReadOnlyList<IReadOnlyList<CondicaoRegraPadraoDto>> Quando);

/// <summary>Uma condição de regra padrão: fato, operador em token canônico e valor JSON.</summary>
public sealed record CondicaoRegraPadraoDto(string Fato, string Operador, JsonElement Valor);
