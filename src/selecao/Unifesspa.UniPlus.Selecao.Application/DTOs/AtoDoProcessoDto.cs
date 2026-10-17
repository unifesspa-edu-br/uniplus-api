namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Documento de um ato do processo, como a consulta administrativa o mostra. Nada aqui endereça o
/// armazenamento: o arquivo se abre pelo acesso do documento a publicar.
/// </summary>
/// <param name="DocumentoAPublicarId">O documento a publicar confirmado.</param>
/// <param name="NomeArquivo">Nome do arquivo enviado.</param>
/// <param name="NomeDeExibicao">Nome com que o anexo se apresenta ao público; nulo no documento principal.</param>
/// <param name="TipoDeMidia">Tipo de mídia do arquivo.</param>
/// <param name="HashSha256">Hash do conteúdo, calculado no servidor na confirmação.</param>
public sealed record DocumentoDoAtoDto(
    Guid DocumentoAPublicarId,
    string NomeArquivo,
    string? NomeDeExibicao,
    string TipoDeMidia,
    string? HashSha256);

/// <summary>
/// Um ato do processo na consulta administrativa: o que criou uma versão do edital ou o registro de
/// um ato que não cria.
/// </summary>
/// <remarks>
/// O ato que cria versão não tem tipo, número, data nem assinante aqui: esses dados são do registro
/// central de atos, e a Seleção referencia o ato só pelo identificador e pelo hash do documento.
/// </remarks>
/// <param name="Id">Identificador do ato.</param>
/// <param name="CriaVersao"><see langword="true"/> quando o ato criou uma versão de configuração do edital.</param>
/// <param name="NumeroVersao">Versão que o ato criou; nulo no ato que não cria versão.</param>
/// <param name="TipoCodigo">Código do tipo do ato; nulo no ato que cria versão.</param>
/// <param name="TipoNome">Nome do tipo do ato; nulo no ato que cria versão.</param>
/// <param name="Numero">Número do ato.</param>
/// <param name="DataDoAto">Data que o documento declara; nula no ato que cria versão.</param>
/// <param name="PublicadoEm">Instante em que o ato foi publicado, pelo relógio do sistema — o que ordena a lista.</param>
/// <param name="Assinante">Quem assina o ato; nulo no ato que cria versão.</param>
/// <param name="Situacao">Pendente até o registro central confirmar o ato.</param>
/// <param name="RegistradoEm">Instante da confirmação; nulo enquanto pendente.</param>
/// <param name="PendenciaAcimaDoLimiar">
/// <see langword="true"/> quando o ato está pendente há mais que o limiar configurado — a pendência
/// que pede atenção, porque a confirmação nunca vem sozinha depois de uma recusa.
/// </param>
/// <param name="AtoCorrigidoId">O ato que este corrige.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção.</param>
/// <param name="ProdutoDoEditalId">Produto do edital que o ato cumpre, quando é resultado declarado.</param>
/// <param name="PoliticaDeDivulgacaoConfirmada">O administrador confirmou a política de divulgação na publicação.</param>
/// <param name="DocumentoPrincipal">Documento principal; nulo quando não foi possível identificá-lo.</param>
/// <param name="Anexos">Anexos, na ordem declarada.</param>
public sealed record AtoDoProcessoDto(
    Guid Id,
    bool CriaVersao,
    int? NumeroVersao,
    string? TipoCodigo,
    string? TipoNome,
    string? Numero,
    DateOnly? DataDoAto,
    DateTimeOffset PublicadoEm,
    string? Assinante,
    SituacaoDoRegistroDoAto Situacao,
    DateTimeOffset? RegistradoEm,
    bool PendenciaAcimaDoLimiar,
    Guid? AtoCorrigidoId,
    string? MotivoDaCorrecao,
    Guid? ProdutoDoEditalId,
    bool PoliticaDeDivulgacaoConfirmada,
    DocumentoDoAtoDto? DocumentoPrincipal,
    IReadOnlyList<DocumentoDoAtoDto> Anexos);
