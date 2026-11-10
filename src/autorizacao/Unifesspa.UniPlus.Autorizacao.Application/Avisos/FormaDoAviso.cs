namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>Como o fato do aviso aconteceu, para os avisos que mudam de texto conforme a forma.</summary>
public enum FormaDoAviso
{
    /// <summary>A migração da concessão foi recusada.</summary>
    Recusa = 1,

    /// <summary>O prazo da migração terminou.</summary>
    FimDoPrazo = 2,

    /// <summary>A gestão de acessos encerrou a migração antes do prazo.</summary>
    EncerramentoManual = 3,

    /// <summary>A versão foi substituída por outra que não abre migração.</summary>
    VersaoSubstituida = 4,

    /// <summary>A versão nova acrescenta permissão, mas nenhuma concessão elegível a migrar.</summary>
    SemConcessaoAMigrar = 5,

    /// <summary>A migração terminou porque não restou concessão pendente.</summary>
    JanelaConcluida = 6,
}
