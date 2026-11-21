namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum FormatoDeAula
{
    [CodigoDeContrato("em_sala", "Em sala")]
    Presencial = 1,

    [CodigoDeContrato("a_distancia", "À distância")]
    Remoto = 2,
}

public enum SituacaoDaTurma
{
    [CodigoDeContrato("aberta", "Aberta")]
    Aberta = 1,

    [CodigoDeContrato("encerrada", "Encerrada")]
    Encerrada = 2,
}

public enum TurnoDeAula
{
    Manha = 1,
    Tarde = 2,
}

public enum CodigoComAspa
{
    [CodigoDeContrato("ordem_z", "Ordem Z")]
    Ultimo = 1,

    [CodigoDeContrato("d'agua", "D'água")]
    ComAspa = 2,

    [CodigoDeContrato("ordem_a", "Ordem A")]
    Primeiro = 3,
}
