namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Códigos das recusas que acontecem <b>antes</b> de existir comando: a carga não desserializa,
/// ou o modelo não passa no binding. São de todo o monólito, porque o mecanismo é do framework
/// e não de nenhum módulo.
/// </summary>
public static class RequisicaoInvalidaErrorCodes
{
    /// <summary>A carga omitiu campo que o contrato declara obrigatório.</summary>
    public const string CampoObrigatorioAusente = "Requisicao.CampoObrigatorioAusente";

    /// <summary>A carga não pôde ser lida: JSON inválido, tipo incompatível, rota que não converte.</summary>
    public const string Malformada = "Requisicao.Malformada";
}
