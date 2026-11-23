namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a data de referência de um documento precisa saber de uma fase: quem ela é, se coleta inscrição e quando
/// começa e termina. A fase do processo em edição e a fase congelada na versão publicada respondem do mesmo jeito.
/// </summary>
public interface IPeriodoDaFase
{
    Guid Id { get; }

    bool ColetaInscricao { get; }

    DateTimeOffset? Inicio { get; }

    DateTimeOffset? Fim { get; }
}
