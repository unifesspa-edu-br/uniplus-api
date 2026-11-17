namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro gerais do documento de configuração do processo. Os de forma (<see cref="FormaInvalida"/> é o grupo, e os demais
/// apontam a causa de cada item) são o que o esquema e os limites do formato recusam, antes de qualquer regra de negócio, e impedem
/// salvar. <see cref="ChaveCitadaInexistente"/> é a causa de configuração: o documento tem a forma certa, a leitura não encontra a
/// parte que uma chave cita, e a recusa só impede publicar.
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

    /// <summary>A chave que uma parte cita não é a de nenhuma parte do tipo citado no documento; recusa publicar, não salvar.</summary>
    public const string ChaveCitadaInexistente = "DocumentoDaConfiguracao.ChaveCitadaInexistente";
}
