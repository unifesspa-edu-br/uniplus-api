namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Enums;

using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// Um intervalo de tempo com extremos opcionais, sempre em UTC. O offset com que o instante chegou é forma
/// de transporte, e não dado de domínio; a comparação é por instante.
/// </summary>
public sealed record JanelaDeTempo
{
    public JanelaDeTempo(DateTimeOffset? inicio, DateTimeOffset? fim)
    {
        Inicio = EmUtc(inicio);
        Fim = EmUtc(fim);
    }

    public DateTimeOffset? Inicio { get; }

    public DateTimeOffset? Fim { get; }

    public bool Completa => Inicio.HasValue && Fim.HasValue;

    public bool Invertida => Completa && Fim < Inicio;

    /// <summary>O primeiro extremo que falta (<c>inicio</c> ou <c>fim</c>), ou nulo quando a janela é completa.</summary>
    public string? ExtremoAusente => ExtremoQueFalta();

    /// <summary>Converte para UTC e descarta a fração de microssegundo que o banco não guarda.</summary>
    public static DateTimeOffset? EmUtc(DateTimeOffset? instante) =>
        instante?.ToUniversalTime().TruncadoEmMicrossegundos();

    public PosicaoNaJanela PosicaoDe(DateTimeOffset instante)
    {
        if (Inicio is { } inicio && instante < inicio)
        {
            return PosicaoNaJanela.Antes;
        }

        return Fim is { } fim && instante > fim ? PosicaoNaJanela.Depois : PosicaoNaJanela.Dentro;
    }

    private string? ExtremoQueFalta()
    {
        if (Inicio is null)
        {
            return "inicio";
        }

        return Fim is null ? "fim" : null;
    }
}
