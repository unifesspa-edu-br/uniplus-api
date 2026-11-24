namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum FormatoDeAula
{
    [CodigoDeContrato("EM_SALA", "Em sala")]
    Presencial = 1,

    [CodigoDeContrato("A_DISTANCIA", "À distância")]
    Remoto = 2,
}

public enum SituacaoDaTurma
{
    [CodigoDeContrato("ABERTA", "Aberta")]
    Aberta = 1,

    [CodigoDeContrato("ENCERRADA", "Encerrada")]
    Encerrada = 2,
}

public enum TurnoDeAula
{
    Manha = 1,
    Tarde = 2,
}

public enum CodigoForaDeOrdem
{
    [CodigoDeContrato("ORDEM_Z", "Ordem Z")]
    Ultimo = 1,

    [CodigoDeContrato("D_AGUA", "D'água")]
    DoMeio = 2,

    [CodigoDeContrato("ORDEM_A", "Ordem A")]
    Primeiro = 3,
}
