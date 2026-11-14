namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

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
/// Um ato do processo na consulta administrativa: o edital de abertura, a retificação, o aviso, a
/// errata ou o resultado.
/// </summary>
/// <param name="Id">Identificador do ato.</param>
/// <param name="TipoCodigo">Código do tipo do ato.</param>
/// <param name="TipoNome">Nome do tipo do ato.</param>
/// <param name="Numero">Número do ato.</param>
/// <param name="DataDoAto">Data que o documento declara; não conta para prazo, vigência nem escolha do tipo de ato.</param>
/// <param name="PublicadoEm">Instante em que o ato foi publicado, pelo relógio do sistema — o que ordena a lista.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="AtoCorrigidoId">O ato que este corrige ou retifica.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção.</param>
/// <param name="PoliticaDeDivulgacaoConfirmada">A política de divulgação foi confirmada na publicação.</param>
/// <param name="DocumentoPrincipal">Documento principal.</param>
/// <param name="Anexos">Anexos, na ordem declarada.</param>
public sealed record AtoDoProcessoDto(
    Guid Id,
    string TipoCodigo,
    string TipoNome,
    string? Numero,
    DateOnly DataDoAto,
    DateTimeOffset PublicadoEm,
    string Assinante,
    Guid? AtoCorrigidoId,
    string? MotivoDaCorrecao,
    bool PoliticaDeDivulgacaoConfirmada,
    DocumentoDoAtoDto DocumentoPrincipal,
    IReadOnlyList<DocumentoDoAtoDto> Anexos);
