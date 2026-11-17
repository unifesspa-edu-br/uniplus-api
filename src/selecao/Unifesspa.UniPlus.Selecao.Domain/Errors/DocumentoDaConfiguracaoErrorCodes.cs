namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro da forma do documento de configuração do processo: o que o esquema e os limites do formato recusam, antes
/// de qualquer regra de negócio. <see cref="FormaInvalida"/> é o grupo; os demais de forma apontam a causa de cada item.
/// </summary>
public static class DocumentoDaConfiguracaoErrorCodes
{
    public const string FormaInvalida = "DocumentoDaConfiguracao.FormaInvalida";
    public const string CampoDesconhecido = "DocumentoDaConfiguracao.CampoDesconhecido";
    public const string TipoInvalido = "DocumentoDaConfiguracao.TipoInvalido";
    public const string ValorInvalido = "DocumentoDaConfiguracao.ValorInvalido";
    public const string CampoObrigatorioAusente = "DocumentoDaConfiguracao.CampoObrigatorioAusente";
    public const string VersaoDoFormatoNaoSuportada = "DocumentoDaConfiguracao.VersaoDoFormatoNaoSuportada";
    public const string PropriedadeRepetida = "DocumentoDaConfiguracao.PropriedadeRepetida";
    public const string ChaveRepetida = "DocumentoDaConfiguracao.ChaveRepetida";
    public const string AcimaDoLimite = "DocumentoDaConfiguracao.AcimaDoLimite";
}
