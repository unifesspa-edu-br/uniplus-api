namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Onde um item do catálogo está no circuito de aprovação da proteção de dados (ADR-0153): a situação,
/// os instantes de cada passo e a classificação que a proteção de dados recomendou ao negar. Quem agiu
/// fica registrado e não sai na resposta.
/// </summary>
/// <param name="Situacao">Token canônico: <c>PENDENTE</c>, <c>SOLICITADA</c>, <c>APROVADA</c> ou <c>NEGADA</c>.</param>
/// <param name="ClassificacaoRecomendada">Token canônico da classificação, quando o item foi negado.</param>
public sealed record AprovacaoDaProtecaoDto(
    string Situacao,
    DateTimeOffset? SolicitadaEm,
    DateTimeOffset? DecididaEm,
    string? ClassificacaoRecomendada,
    DateTimeOffset? AceitaEm);

/// <summary>Um tipo de documento ou fato do catálogo com a proteção que aguarda aprovação.</summary>
/// <param name="Tipo"><c>TIPO_DOCUMENTO</c> ou <c>FATO</c>.</param>
public sealed record ItemDaProtecaoDoCatalogoDto(
    string Tipo,
    Guid Id,
    string Codigo,
    string Nome,
    string ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal,
    AprovacaoDaProtecaoDto Aprovacao);

/// <summary>O que a solicitação levou à proteção de dados: a quantidade de itens de cada cadastro.</summary>
public sealed record SolicitacaoDaProtecaoDto(int TiposDeDocumento, int Fatos);
