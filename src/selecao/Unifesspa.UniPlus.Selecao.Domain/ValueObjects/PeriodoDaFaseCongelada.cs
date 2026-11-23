namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A janela de uma fase como a versão publicada a congelou, lida do documento sem reconstituir a fase inteira.</summary>
public sealed record PeriodoDaFaseCongelada(Guid Id, bool ColetaInscricao, DateTimeOffset? Inicio, DateTimeOffset? Fim) : IPeriodoDaFase;
