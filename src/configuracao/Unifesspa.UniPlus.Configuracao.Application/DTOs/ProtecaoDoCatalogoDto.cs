namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Onde um item do catálogo está no circuito de aprovação da proteção de dados (ADR-0153): a situação,
/// os instantes de cada passo, a classificação que a proteção de dados recomendou no ajuste e a justificativa
/// da decisão. Quem agiu fica registrado e não sai na resposta.
/// </summary>
/// <param name="Situacao">Token canônico: <c>PENDENTE</c>, <c>SOLICITADA</c>, <c>APROVADA</c>, <c>AJUSTE_RECOMENDADO</c> ou <c>REPROVADA</c>.</param>
/// <param name="ClassificacaoRecomendada">Token canônico da classificação, quando o ajuste foi recomendado.</param>
/// <param name="Justificativa">Motivo da decisão, quando a proteção de dados o escreveu; sempre presente na reprovação.</param>
public sealed record AprovacaoDaProtecaoDto(
    string Situacao,
    DateTimeOffset? SolicitadaEm,
    DateTimeOffset? DecididaEm,
    string? ClassificacaoRecomendada,
    string? Justificativa,
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

/// <summary>Um item do catálogo que o administrador escolheu enviar à proteção de dados.</summary>
/// <param name="Cadastro"><c>TIPO_DOCUMENTO</c> ou <c>FATO</c>; comparado sem os espaços das pontas.</param>
/// <param name="Id">Identificador do tipo de documento ou do fato.</param>
public sealed record ItemParaSolicitarDto(string? Cadastro, Guid Id);

/// <summary>O que a solicitação levou à proteção de dados: a quantidade de itens de cada cadastro.</summary>
public sealed record SolicitacaoDaProtecaoDto(int TiposDeDocumento, int Fatos);

/// <summary>
/// O que a proteção de dados precisa ver para decidir sobre um item do catálogo (ADR-0153): a proteção proposta, a
/// situação da aprovação e o que o item é. Exatamente um dos blocos <paramref name="Fato"/> e
/// <paramref name="TipoDocumento"/> vem preenchido, conforme o <paramref name="Tipo"/>. Quem enviou ou decidiu
/// fica registrado e não sai na resposta.
/// </summary>
/// <param name="Tipo"><c>TIPO_DOCUMENTO</c> ou <c>FATO</c>.</param>
/// <param name="Fato">O que é o fato; nulo no tipo de documento.</param>
/// <param name="TipoDocumento">O que o tipo de documento pede ao candidato; nulo no fato.</param>
public sealed record DetalheDaProtecaoDoCatalogoDto(
    string Tipo,
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string ClassificacaoProtecao,
    string HipoteseLegal,
    string FinalidadeTratamento,
    AprovacaoDaProtecaoDto Aprovacao,
    DetalheDoFatoDto? Fato,
    DetalheDoTipoDeDocumentoDto? TipoDocumento);

/// <summary>O fato, para quem avalia a proteção: o tipo do dado, como é exibido e os valores possíveis.</summary>
/// <param name="Dominio">Token canônico do tipo do dado.</param>
/// <param name="Cardinalidade">Token canônico: se o fato recebe um valor ou vários.</param>
/// <param name="Sistema"><c>true</c> no fato que o sistema mantém.</param>
/// <param name="ComponenteExibicao">Token canônico do componente de exibição, quando o fato declara um.</param>
/// <param name="Valores">Os valores possíveis ativos, na ordem do cadastro; vazio no fato sem conjunto fechado.</param>
public sealed record DetalheDoFatoDto(
    string Dominio,
    string Cardinalidade,
    bool Sistema,
    string? ComponenteExibicao,
    IReadOnlyList<DetalheDoValorDoFatoDto> Valores);

/// <summary>Um valor possível do fato: o código, o rótulo e a orientação ao candidato.</summary>
public sealed record DetalheDoValorDoFatoDto(string Codigo, string? Descricao, string? Orientacao);

/// <summary>O tipo de documento, para quem avalia a proteção: a categoria e o que é pedido ao candidato.</summary>
/// <param name="Categoria">Código da categoria do documento no cadastro de categorias.</param>
/// <param name="FormatosAceitos">Formatos de arquivo aceitos, quando o cadastro os define.</param>
/// <param name="TamanhoMaximoMb">Tamanho máximo do arquivo em megabytes, quando o cadastro o define.</param>
public sealed record DetalheDoTipoDeDocumentoDto(string Categoria, string? FormatosAceitos, int? TamanhoMaximoMb);
