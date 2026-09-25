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
    public const string IdentificadorLegivelEmUso = "ProcessoSeletivo.IdentificadorLegivelEmUso";
}
