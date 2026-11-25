namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>A etapa em que a decisão sobre uma concessão da revisão foi tomada.</summary>
public enum EtapaDaMigracao
{
    /// <summary>A gestão de acessos, sempre a primeira.</summary>
    [CodigoDeContrato("GESTAO_DE_ACESSOS", "Gestão de acessos")]
    GestaoDeAcessos = 1,

    /// <summary>A proteção de dados, depois da gestão, quando o acréscimo é pessoal, identificador ou sensível.</summary>
    [CodigoDeContrato("PROTECAO_DE_DADOS", "Proteção de dados")]
    ProtecaoDeDados = 2,

    /// <summary>O sistema, que retém a concessão do gestor e encerra a que ninguém decidiu.</summary>
    [CodigoDeContrato("SISTEMA", "Sistema")]
    Sistema = 3,
}
