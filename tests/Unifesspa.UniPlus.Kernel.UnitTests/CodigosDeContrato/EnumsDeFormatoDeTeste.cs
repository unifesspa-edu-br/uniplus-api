namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum FormatoValido
{
    [CodigoDeContrato("VALIDO", "Rótulo")]
    Membro = 1,
}

public enum FormatoFaseFinal
{
    [CodigoDeContrato("FASE2_FINAL", "Rótulo")]
    Membro = 1,
}

public enum FormatoVariasPartes
{
    [CodigoDeContrato("UMA_PARTE_OUTRA_3", "Rótulo")]
    Membro = 1,
}

public enum FormatoOitentaCaracteres
{
    [CodigoDeContrato("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "Rótulo")]
    Membro = 1,
}

public enum FormatoOitentaEUmCaracteres
{
    [CodigoDeContrato("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "Rótulo")]
    Membro = 1,
}

public enum FormatoLongoForaDoFormato
{
    [CodigoDeContrato("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Rótulo")]
    Membro = 1,
}

public enum FormatoEspacoNoInicio
{
    [CodigoDeContrato(" ABC", "Rótulo")]
    Membro = 1,
}

public enum FormatoEspacoNoFim
{
    [CodigoDeContrato("abc ", "Rótulo")]
    Membro = 1,
}

public enum FormatoSomenteEspacos
{
    [CodigoDeContrato("   ", "Rótulo")]
    Membro = 1,
}

public enum FormatoVazio
{
    [CodigoDeContrato("", "Rótulo")]
    Membro = 1,
}

public enum FormatoLongoComEspaco
{
    [CodigoDeContrato(" AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "Rótulo")]
    Membro = 1,
}

public enum FormatoMinuscula
{
    [CodigoDeContrato("minuscula", "Rótulo")]
    Membro = 1,
}

public enum FormatoAcentuacao
{
    [CodigoDeContrato("ACENTUAÇÃO", "Rótulo")]
    Membro = 1,
}

public enum FormatoHifen
{
    [CodigoDeContrato("COM-HIFEN", "Rótulo")]
    Membro = 1,
}

public enum FormatoSublinhados
{
    [CodigoDeContrato("DOIS__SUBLINHADOS", "Rótulo")]
    Membro = 1,
}

public enum FormatoSublinhadoNoInicio
{
    [CodigoDeContrato("_INICIO", "Rótulo")]
    Membro = 1,
}

public enum FormatoSublinhadoNoFim
{
    [CodigoDeContrato("FINAL_", "Rótulo")]
    Membro = 1,
}

public enum FormatoNumeroNoInicio
{
    [CodigoDeContrato("1INICIO", "Rótulo")]
    Membro = 1,
}

public enum FormatoEspacoNoMeio
{
    [CodigoDeContrato("ESPACO NO MEIO", "Rótulo")]
    Membro = 1,
}
