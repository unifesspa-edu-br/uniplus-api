namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// O instante em que o comando publica, lido uma só vez do relógio, e o dia civil dele no fuso institucional.
/// </summary>
/// <param name="Instante">O instante da divulgação: vigência da versão, registro do ato do processo, ato no registro central e divulgação do certame.</param>
/// <param name="Dia">O dia civil de <paramref name="Instante"/> no fuso institucional, o dia em que o tipo de ato vigora.</param>
public readonly record struct InstanteDaPublicacao(DateTimeOffset Instante, DateOnly Dia);

/// <summary>
/// Lê o relógio e resolve o dia civil do instante lido. Cada comando que publica o chama uma vez, depois de tomar a
/// trava do processo: versão, ato e divulgação valem o mesmo instante, e a data que o documento declara não
/// participa da escolha do tipo de ato.
/// </summary>
public sealed class LeituraDoInstanteDaPublicacao(TimeProvider relogio, IResolvedorFusoInstitucional fuso)
{
    public Result<InstanteDaPublicacao> Ler()
    {
        DateTimeOffset instante = relogio.GetUtcNow();
        Result<DateOnly> dia = fuso.DiaCivilDe(instante);

        return dia.IsFailure
            ? Result<InstanteDaPublicacao>.Failure(dia.Error!)
            : Result<InstanteDaPublicacao>.Success(new InstanteDaPublicacao(instante, dia.Value));
    }
}
