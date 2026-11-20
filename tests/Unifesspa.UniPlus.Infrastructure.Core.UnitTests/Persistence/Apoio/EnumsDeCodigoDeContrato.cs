namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum FormatoDeAula
{
    [CodigoDeContrato("em_sala", "Em sala")]
    Presencial = 1,

    [CodigoDeContrato("a_distancia", "À distância")]
    Remoto = 2,
}

public enum TurnoDeAula
{
    Manha = 1,
    Tarde = 2,
}
