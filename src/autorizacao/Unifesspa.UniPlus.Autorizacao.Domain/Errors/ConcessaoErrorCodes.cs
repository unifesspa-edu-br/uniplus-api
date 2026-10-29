namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da concessão de acesso, declarados uma vez para que o domínio
/// e o registro de erros da API não divirjam.
/// </summary>
public static class ConcessaoErrorCodes
{
    public const string NaoEncontrada = "Concessao.NaoEncontrada";
    public const string JaRevogada = "Concessao.JaRevogada";
    public const string MinimoDeGestores = "Concessao.MinimoDeGestores";
    public const string MotivoInvalido = "Concessao.MotivoInvalido";
    public const string PerfilInativo = "Concessao.PerfilInativo";
    public const string Duplicada = "Concessao.Duplicada";
    public const string ValidadeInvalida = "Concessao.ValidadeInvalida";
    public const string EscopoInvalido = "Concessao.EscopoInvalido";
    public const string AutorNaoIdentificado = "Concessao.AutorNaoIdentificado";
    public const string ConflitoDeConcorrencia = "Concessao.ConflitoDeConcorrencia";
    public const string ConfirmacaoDivergente = "Concessao.ConfirmacaoDivergente";
    public const string FiltroDePessoaInvalido = "Concessao.FiltroDePessoaInvalido";
    public const string FiltroDeSituacaoInvalido = "Concessao.FiltroDeSituacaoInvalido";
}
