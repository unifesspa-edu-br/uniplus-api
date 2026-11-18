namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// O instante em que o comando age, lido uma só vez do relógio, e o dia civil dele no fuso institucional.
/// </summary>
/// <param name="Instante">O instante do comando: na publicação, a vigência da versão, o registro do ato do processo, o ato no registro central e a divulgação do certame; na leitura de um documento, o instante único dos cadastros lidos.</param>
/// <param name="Dia">O dia civil de <paramref name="Instante"/> no fuso institucional, o dia em que o tipo de ato vigora.</param>
public readonly record struct InstanteDoComando(DateTimeOffset Instante, DateOnly Dia);

/// <summary>
/// Lê o relógio e resolve o dia civil do instante lido. Cada comando o chama uma vez, depois de tomar o bloqueio do processo,
/// quando há: tudo o que o comando decide vale o mesmo instante, e a data que o documento declara não participa da escolha do
/// tipo de ato.
/// </summary>
public sealed class LeituraDoInstanteDoComando(TimeProvider relogio, IResolvedorFusoInstitucional fuso)
{
    public Result<InstanteDoComando> Ler()
    {
        DateTimeOffset instante = relogio.GetUtcNow();
        Result<DateOnly> dia = fuso.DiaCivilDe(instante);

        return dia.IsFailure
            ? Result<InstanteDoComando>.Failure(dia.Error!)
            : Result<InstanteDoComando>.Success(new InstanteDoComando(instante, dia.Value));
    }
}
