namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// O fato que pode iniciar uma fase: a fase de origem que ele iniciaria, a versão publicada que se aplica a ele e o
/// instante em que ele passou a contar. A versão aplicável é nula quando a regra não amarra o fato a versão nenhuma: o
/// ato no lugar da fase e a avaliação iniciada na fase valem para a fase pela identidade dela. Para o fato do período, o
/// instante é o maior entre a data declarada e o início da vigência da versão que a declarou.
/// </summary>
public sealed record FatoQuePodeIniciarAFase(Guid FaseCanonicaOrigemId, FasesDaVersaoPublicada? VersaoAplicavel, DateTimeOffset Instante);
