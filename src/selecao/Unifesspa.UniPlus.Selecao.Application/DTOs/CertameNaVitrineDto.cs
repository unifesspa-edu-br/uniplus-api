namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

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
/// <param name="IdentificadorLegivel">
/// Endereço público do certame, congelado na publicação — o mesmo pelo qual o detalhe o localiza.
/// </param>
/// <param name="Numero">Identificador legível do edital. Nem toda publicação o declara.</param>
/// <param name="Nome">Título do certame.</param>
/// <param name="TipoProcesso">Tipo do processo, como congelado na publicação.</param>
/// <param name="ModalidadesOfertadas">Códigos das modalidades com vagas no certame.</param>
/// <param name="InscricoesDe">
/// Abertura da janela de inscrição, lida da mesma versão publicamente visível. Está aqui porque é
/// o que dá sentido à situação "em breve": anunciar que o certame ainda vai abrir sem dizer quando
/// é um aviso que não informa nada.
/// </param>
/// <param name="InscricoesAte">
/// Encerramento da janela de inscrição, lido da versão publicamente visível — a mesma que a página
/// do certame serve. Não da publicação mais nova: enquanto o ato de uma retificação não se
/// registra, ela não tem publicidade nenhuma, e anunciar aqui o prazo dela seria dar-lhe uma.
/// </param>
/// <param name="Situacao">
/// Em que ponto da janela de inscrição o certame está, no instante da consulta. Resolvido no
/// servidor, e não pelo cliente a partir da data: o fuso de quem lê não decide prazo de edital.
/// <para>
/// É a MESMA palavra pela qual a vitrine filtra e conta. Filtrar por uma situação devolve
/// exatamente os itens que a trazem aqui, e o contador daquela situação é quantos são — a marca do
/// item, o recorte e o número não podem discordar porque são o mesmo enunciado.
/// </para>
/// </param>
/// <param name="TotalDeVagas">Soma das vagas publicadas em todas as ofertas do certame.</param>
public sealed record CertameNaVitrineDto(
    Guid ProcessoSeletivoId,
    string IdentificadorLegivel,
    string? Numero,
    string Nome,
    TipoCatalogadoCertameDto TipoProcesso,
    IReadOnlyList<string> ModalidadesOfertadas,
    DateTimeOffset InscricoesDe,
    DateTimeOffset InscricoesAte,
    SituacaoDoCertame Situacao,
    int TotalDeVagas);
