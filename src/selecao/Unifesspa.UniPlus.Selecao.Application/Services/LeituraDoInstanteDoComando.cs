namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O instante em que o comando age, lido uma só vez do relógio, e o dia civil dele no fuso institucional.
/// </summary>
/// <param name="Instante">O instante do comando: na publicação e na retificação, a vigência da versão, o registro do ato do processo, o ato no registro central e a divulgação do certame, e o instante em que a retificação confere os prazos e monta os fatos; na leitura de um documento, o instante único dos cadastros lidos.</param>
/// <param name="Dia">O dia civil de <paramref name="Instante"/> no fuso institucional, o dia em que o tipo de ato vigora.</param>
public readonly record struct InstanteDoComando(DateTimeOffset Instante, DateOnly Dia);

/// <summary>
/// Lê o relógio e resolve o dia civil do instante lido. Cada comando o chama uma vez, depois de tomar o bloqueio do processo,
/// quando há: tudo o que o comando decide vale o mesmo instante, e a data que o documento declara não participa da escolha do
/// tipo de ato. A retificação lê o instante efetivo, que nunca precede a vigência da versão que ela sucede.
/// </summary>
public sealed class LeituraDoInstanteDoComando(TimeProvider relogio, IResolvedorFusoInstitucional fuso)
{
    public Result<InstanteDoComando> Ler() => LerEm(relogio.GetUtcNow());

    /// <summary>
    /// O instante efetivo de quem sucede <paramref name="versaoAnterior"/>: o maior entre o relógio e a vigência dela. O
    /// relógio pode regredir, e a vigência da versão nova nunca precede a da anterior; ler o instante já com essa regra
    /// faz a vigência emitida e o instante das conferências do comando serem o mesmo.
    /// </summary>
    public Result<InstanteDoComando> LerNaSucessaoDe(VersaoConfiguracao versaoAnterior)
    {
        ArgumentNullException.ThrowIfNull(versaoAnterior);

        return LerEm(versaoAnterior.VigenciaDaSucessora(relogio.GetUtcNow()));
    }

    private Result<InstanteDoComando> LerEm(DateTimeOffset instante)
    {
        Result<DateOnly> dia = fuso.DiaCivilDe(instante);

        return dia.IsFailure
            ? Result<InstanteDoComando>.Failure(dia.Error!)
            : Result<InstanteDoComando>.Success(new InstanteDoComando(instante, dia.Value));
    }
}
