namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que o veredicto "verificado" de um documento de versão produziu no perfil.</summary>
public enum DesfechoDoDocumentoVerificado
{
    /// <summary>Resultado repetido, ou documento que não esperava veredicto: nada mudou.</summary>
    Ignorado = 1,

    /// <summary>O documento foi gravado, mas a versão já estava cancelada ou recusada e nada mais mudou.</summary>
    Gravado = 2,

    /// <summary>A versão pendente passou a ser a composição em vigor do perfil.</summary>
    EntrouEmVigor = 3,

    /// <summary>O documento foi gravado e a versão foi recusada por uma conferência da entrada em vigor.</summary>
    VersaoRecusada = 4,
}
