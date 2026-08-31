namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

public static class TipoDocumentoErrorCodes
{
    public const string CodigoObrigatorio = "TipoDocumento.CodigoObrigatorio";

    /// <summary>
    /// Código fora do formato fechado <c>^[A-Z][A-Z0-9_]{1,49}$</c>
    /// (<see cref="Unifesspa.UniPlus.Configuracao.Domain.ValueObjects.CodigoTipoDocumento"/>).
    /// Cobre também o comprimento, de 2 a 50 caracteres.
    /// </summary>
    public const string CodigoFormatoInvalido = "TipoDocumento.CodigoFormatoInvalido";
    public const string CodigoJaExiste = "TipoDocumento.CodigoJaExiste";
    public const string NomeObrigatorio = "TipoDocumento.NomeObrigatorio";
    public const string NomeTamanho = "TipoDocumento.NomeTamanho";
    public const string DescricaoTamanho = "TipoDocumento.DescricaoTamanho";
    public const string CategoriaObrigatoria = "TipoDocumento.CategoriaObrigatoria";
    public const string CategoriaFormatoInvalido = "TipoDocumento.CategoriaFormatoInvalido";

    /// <summary>
    /// Nenhuma categoria viva com o código informado — checagem do handler contra o
    /// cadastro, não do agregado, que valida só a forma (ADR-0125).
    /// </summary>
    public const string CategoriaNaoEncontrada = "TipoDocumento.CategoriaNaoEncontrada";
    public const string FormatosAceitosTamanho = "TipoDocumento.FormatosAceitosTamanho";
    public const string TamanhoMaximoInvalido = "TipoDocumento.TamanhoMaximoInvalido";
    public const string TipoEquivalenteTamanho = "TipoDocumento.TipoEquivalenteTamanho";
    public const string TipoEquivalenteIgualCodigo = "TipoDocumento.TipoEquivalenteIgualCodigo";
    public const string NaoEncontrado = "TipoDocumento.NaoEncontrado";
}
