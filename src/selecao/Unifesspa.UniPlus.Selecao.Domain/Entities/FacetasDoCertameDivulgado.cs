namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que a vitrine precisa <b>consultar</b> sobre um certame divulgado: o que ela busca, filtra e
/// ordena.
/// </summary>
/// <remarks>
/// Os mesmos valores que o documento guardado carrega, replicados em coluna porque buscar texto,
/// recortar por modalidade e ordenar por prazo exigem coluna e índice. Não são segunda fonte: saem
/// da mesma projeção, no mesmo instante, e avançam com ela.
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
