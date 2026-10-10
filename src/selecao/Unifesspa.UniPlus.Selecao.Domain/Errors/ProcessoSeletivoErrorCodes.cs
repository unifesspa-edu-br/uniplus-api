namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do processo seletivo declarados como constantes, e não repetidos
/// como literal no agregado e no registro de erros da API.
/// </summary>
public static class ProcessoSeletivoErrorCodes
{
    public const string CriteriosDesempateEmExcesso = "ProcessoSeletivo.CriteriosDesempateEmExcesso";

    public const string IdentificadorLegivelTamanho = "ProcessoSeletivo.IdentificadorLegivelTamanho";
    public const string IdentificadorLegivelFormatoInvalido = "ProcessoSeletivo.IdentificadorLegivelFormatoInvalido";
    public const string IdentificadorLegivelComFormatoDeGuid = "ProcessoSeletivo.IdentificadorLegivelComFormatoDeGuid";
    public const string IdentificadorLegivelAusente = "ProcessoSeletivo.IdentificadorLegivelAusente";
    public const string IdentificadorLegivelImutavel = "ProcessoSeletivo.IdentificadorLegivelImutavel";
    public const string BonusRegionalAplicaSemConfiguracao = "ProcessoSeletivo.BonusRegionalAplicaSemConfiguracao";
    public const string BonusRegionalNaoAplicaComConfiguracao = "ProcessoSeletivo.BonusRegionalNaoAplicaComConfiguracao";
    public const string FatoElegibilidadeAusente = "ProcessoSeletivo.FatoElegibilidadeAusente";
    public const string FatoElegibilidadeForaDoProcesso = "ProcessoSeletivo.FatoElegibilidadeForaDoProcesso";
    public const string FatoElegibilidadeDeMembroDeGrupo = "ProcessoSeletivo.FatoElegibilidadeDeMembroDeGrupo";
    public const string FatoElegibilidadeNaoBooleano = "ProcessoSeletivo.FatoElegibilidadeNaoBooleano";
    public const string FatoElegibilidadeConhecidoDepoisDaInscricao = "ProcessoSeletivo.FatoElegibilidadeConhecidoDepoisDaInscricao";
    public const string FatoElegibilidadeDaIsencao = "ProcessoSeletivo.FatoElegibilidadeDaIsencao";
    public const string IdentificadorLegivelEmUso = "ProcessoSeletivo.IdentificadorLegivelEmUso";
    public const string RecursoAcessibilidadeReferenciadoPorCondicaoViva = "ProcessoSeletivo.RecursoAcessibilidadeReferenciadoPorCondicaoViva";
}
