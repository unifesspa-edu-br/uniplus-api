namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que a vitrine precisa <b>consultar</b> sobre um certame divulgado: o que ela busca, filtra e
/// ordena.
/// </summary>
/// <remarks>
/// <para>
/// A resposta pública inteira fica guardada como documento, e é ela que se serve. Estes campos são
/// os mesmos valores, replicados em colunas, porque consulta não se faz sobre documento: buscar
/// texto, recortar por modalidade e ordenar por prazo exigem coluna e índice.
/// </para>
/// <para>
/// Não são uma segunda fonte. Saem da mesma projeção que produz o documento, no mesmo instante, e
/// avançam com ele — não há caminho que atualize um sem o outro.
/// </para>
/// </remarks>
/// <param name="Nome">Título congelado do certame — o que a busca encontra e o que ordena alfabeticamente.</param>
/// <param name="Numero">Identificador legível do edital. Nem toda publicação o declara.</param>
/// <param name="ModalidadesOfertadas">Códigos das modalidades com vaga no certame, para o recorte.</param>
/// <param name="InscricoesDe">Abertura da janela de inscrição.</param>
/// <param name="InscricoesAte">Encerramento da janela — a grandeza de que as situações derivam.</param>
public sealed record FacetasDoCertameDivulgado(
    string Nome,
    string? Numero,
    IReadOnlyList<string> ModalidadesOfertadas,
    DateTimeOffset InscricoesDe,
    DateTimeOffset InscricoesAte);
