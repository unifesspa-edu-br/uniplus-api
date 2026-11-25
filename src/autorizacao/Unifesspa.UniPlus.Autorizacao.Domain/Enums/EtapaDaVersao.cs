namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Etapa da aprovação de uma versão de perfil.</summary>
public enum EtapaDaVersao
{
    /// <summary>A gestão de acessos, sempre a primeira.</summary>
    [CodigoDeContrato("GESTAO_DE_ACESSOS", "Gestão de acessos")]
    GestaoDeAcessos = 1,

    /// <summary>A proteção de dados, depois da gestão, quando o acréscimo é pessoal, identificador ou sensível.</summary>
    [CodigoDeContrato("PROTECAO_DE_DADOS", "Proteção de dados")]
    ProtecaoDeDados = 2,
}
