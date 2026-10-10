namespace Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// A edição do ENEM cujas notas a classificação usa: o processo declara as edições que admite, e o
/// candidato escolhe uma delas na inscrição (issue #1620).
/// </summary>
public static class EdicaoDoEnem
{
    /// <summary>O fato de sistema da edição escolhida, cujas opções são as edições declaradas pelo processo.</summary>
    public const string Fato = "EDICAO_ENEM";
}
