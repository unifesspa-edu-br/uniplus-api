namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

internal enum EstadoDaSincronizacaoDiaria
{
    Pendente = 1,
    Despachada = 2,
    Executando = 3,
    Concluida = 4,
    Parcial = 5,
    Falhou = 6,
}

internal sealed class ReservaDeSincronizacaoDiaria
{
    private ReservaDeSincronizacaoDiaria()
    {
    }

    public DateOnly DataDeReferencia { get; private set; }

    public EstadoDaSincronizacaoDiaria Estado { get; private set; }

    public DateTimeOffset AgendadaEm { get; private set; }

    public DateTimeOffset? DespachadaEm { get; private set; }

    public DateTimeOffset? IniciadaEm { get; private set; }

    public DateTimeOffset? ConcluidaEm { get; private set; }

    public void MarcarComoExecutando(DateTimeOffset instante)
    {
        Estado = EstadoDaSincronizacaoDiaria.Executando;
        IniciadaEm = instante;
    }

    public void Concluir(EstadoDaSincronizacaoDiaria estado, DateTimeOffset instante)
    {
        if (estado is not (
            EstadoDaSincronizacaoDiaria.Concluida
            or EstadoDaSincronizacaoDiaria.Parcial
            or EstadoDaSincronizacaoDiaria.Falhou))
        {
            throw new ArgumentOutOfRangeException(nameof(estado), estado, "O estado informado não é terminal.");
        }

        Estado = estado;
        ConcluidaEm = instante;
    }
}
