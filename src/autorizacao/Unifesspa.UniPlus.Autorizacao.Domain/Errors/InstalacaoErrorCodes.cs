namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da instalação dos primeiros gestores, declarados uma vez para que o
/// domínio, os handlers e o registro de erros da API não divirjam.
/// </summary>
public static class InstalacaoErrorCodes
{
    public const string JaHaGestor = "Instalacao.JaHaGestor";
    public const string ExecutorEntreGestores = "Instalacao.ExecutorEntreGestores";
    public const string GestoresInvalidos = "Instalacao.GestoresInvalidos";
    public const string DocumentoInvalido = "Instalacao.DocumentoInvalido";
    public const string SituacaoInvalida = "Instalacao.SituacaoInvalida";
    public const string NaoEncontrada = "Instalacao.NaoEncontrada";
    public const string EmailNaoInstitucional = "Instalacao.EmailNaoInstitucional";
    public const string CodigoInvalido = "Instalacao.CodigoInvalido";
    public const string AutorNaoIdentificado = "Instalacao.AutorNaoIdentificado";
    public const string ConflitoDeConcorrencia = "Instalacao.ConflitoDeConcorrencia";
}
