namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

// Mapeamentos para HTTP registrados em ConfiguracaoDomainErrorRegistration:
//   CodigoJaExiste        → 409 Conflict
//   NaoEncontrada         → 404 Not Found
//   CodigoObrigatorio     → 422 Unprocessable Entity
//   CodigoFormatoInvalido → 422 Unprocessable Entity
//   NomeObrigatorio       → 422 Unprocessable Entity
//   NomeTamanho           → 422 Unprocessable Entity
//   DescricaoTamanho      → 422 Unprocessable Entity
//   OrdemInvalida         → 422 Unprocessable Entity
public static class CategoriaDocumentoErrorCodes
{
    public const string CodigoObrigatorio = "CategoriaDocumento.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "CategoriaDocumento.CodigoFormatoInvalido";
    public const string CodigoJaExiste = "CategoriaDocumento.CodigoJaExiste";
    public const string NomeObrigatorio = "CategoriaDocumento.NomeObrigatorio";
    public const string NomeTamanho = "CategoriaDocumento.NomeTamanho";
    public const string DescricaoTamanho = "CategoriaDocumento.DescricaoTamanho";
    public const string OrdemInvalida = "CategoriaDocumento.OrdemInvalida";
    public const string NaoEncontrada = "CategoriaDocumento.NaoEncontrada";
}
