namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro da regra de homologação das inscrições que o edital declara na fase. Constantes, e
/// não literais repetidos no domínio e no registro de erros da API: um código escrito duas vezes
/// diverge na primeira correção de grafia, e o mapeamento órfão só apareceria como <c>500</c>.
/// </summary>
public static class HomologacaoInscricaoErrorCodes
{
    public const string ResultadoNaoDeclarado = "HomologacaoInscricao.ResultadoNaoDeclarado";
    public const string CondicaoNaoDisponivel = "HomologacaoInscricao.CondicaoNaoDisponivel";
    public const string EdicaoDoExameInvalida = "HomologacaoInscricao.EdicaoDoExameInvalida";
    public const string AlternativaSemCondicao = "HomologacaoInscricao.AlternativaSemCondicao";
    public const string ConfirmacaoDoInepAusente = "HomologacaoInscricao.ConfirmacaoDoInepAusente";
}
