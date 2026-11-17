namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro da resolução dos cadastros que a declaração da fase cita por identificador. Constantes, e não literais
/// repetidos na montagem da Application, no registro dos erros da API e nos testes: um código escrito duas vezes diverge na
/// primeira correção de grafia.
/// </summary>
public static class FaseCronogramaErrorCodes
{
    public const string FaseCanonicaNaoEncontrada = "FaseCronograma.FaseCanonicaNaoEncontrada";
    public const string TipoBancaNaoEncontrado = "FaseCronograma.TipoBancaNaoEncontrado";
    public const string CategoriaDocumentoNaoEncontrada = "FaseCronograma.CategoriaDocumentoNaoEncontrada";
    public const string UnidadeResponsavelNaoEncontrada = "FaseCronograma.UnidadeResponsavelNaoEncontrada";
    public const string AtoDoProdutoNaoEncontrado = "ProdutoDaFase.AtoNaoEncontradoNoCatalogo";
    public const string RegraDeRecursoInvalida = "RegraRecursoFase.RegraCatalogoInvalida";
}
