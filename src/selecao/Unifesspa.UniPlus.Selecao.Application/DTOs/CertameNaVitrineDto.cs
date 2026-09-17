namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Um certame como a vitrine pública o apresenta: o bastante para o cidadão escolher em qual se
/// inscrever, e nada além.
/// </summary>
/// <remarks>
/// Projetado da mesma versão publicamente visível que a página do certame serve — a mais nova, entre
/// as vigentes, cujo ato criador está registrado. Vitrine e detalhe do mesmo processo nunca
/// discordam sobre qual versão estão mostrando.
/// </remarks>
/// <param name="ProcessoSeletivoId">Identificador do processo, por onde se abre o detalhe.</param>
/// <param name="Numero">Identificador legível do edital. Nem toda publicação o declara.</param>
/// <param name="Nome">Título do certame.</param>
/// <param name="TipoProcesso">Tipo do processo, como congelado na publicação.</param>
/// <param name="ModalidadesOfertadas">Códigos das modalidades com vagas no certame.</param>
/// <param name="InscricoesAte">
/// Encerramento da janela de inscrição, lido da versão publicamente visível — a mesma que a página
/// do certame serve. Não da publicação mais nova: enquanto o ato de uma retificação não se
/// registra, ela não tem publicidade nenhuma, e anunciar aqui o prazo dela seria dar-lhe uma.
/// </param>
/// <param name="InscricoesAbertas">
/// Se o certame ainda recebe inscrição no instante da consulta. Resolvido no servidor, e não pelo
/// cliente a partir da data: o fuso de quem lê não decide prazo de edital.
/// </param>
/// <param name="TotalDeVagas">Soma das vagas publicadas em todas as ofertas do certame.</param>
public sealed record CertameNaVitrineDto(
    Guid ProcessoSeletivoId,
    string? Numero,
    string Nome,
    TipoCatalogadoCertameDto TipoProcesso,
    IReadOnlyList<string> ModalidadesOfertadas,
    DateTimeOffset InscricoesAte,
    bool InscricoesAbertas,
    int TotalDeVagas);
