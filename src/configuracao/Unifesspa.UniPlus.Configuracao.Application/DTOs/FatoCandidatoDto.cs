namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json;

/// <summary>
/// Representação de manutenção de um fato do catálogo (ADR-0136): os eixos, a proteção de dados e
/// os valores de domínio, com o estado de cada um, as regras padrão do derivado por regra e o circuito de
/// aprovação da proteção de dados. Os enums saem
/// como token canônico UPPER_SNAKE.
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
    IReadOnlyList<RegraPadraoDto> RegrasPadrao,
    AprovacaoDaProtecaoDto Aprovacao);

/// <summary>Um valor de domínio de um fato de fonte global.</summary>
public sealed record FatoValorDominioDto(string Codigo, string? Descricao, int Ordem, bool Ativo, string? Orientacao);

/// <summary>
/// Uma regra padrão do derivado: o que contribui (nulo no booleano) e o predicado, OU de cláusulas
/// que são E de condições; vazio na regra incondicional.
/// </summary>
public sealed record RegraPadraoDto(string? Contribui, IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoDto>> Quando);

