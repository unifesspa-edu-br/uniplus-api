namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

public static class CodigoDoFormularioErrorCodes
{
    /// <summary>O formulário do processo foi declarado sem código.</summary>
    public const string Obrigatorio = "FormularioProcesso.CodigoObrigatorio";

    /// <summary>O código do formulário não tem letra maiúscula inicial seguida de letras maiúsculas, dígitos e sublinhado, até sessenta caracteres.</summary>
    public const string FormatoInvalido = "FormularioProcesso.CodigoFormatoInvalido";
}
