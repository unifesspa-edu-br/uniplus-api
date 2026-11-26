namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

internal static class DescricaoDoValorDoItem
{
    public const string Texto = "O valor do item, na forma que `tipo` define (a forma de cada código está em `FormaDoValorDoItemCodigo`). O `componente` só diz como a tela mostra o valor, sem mudar a forma. Com tipo `OPCAO` ou `OPCOES`, o valor sai como código e rótulo (uma lista no caso de `OPCOES`). O rótulo pode vir das opções declaradas pelo processo e congeladas na publicação ou dos valores de domínio congelados; para UF, vem da lista oficial. Se não houver rótulo, usa-se o próprio código. No caso de município, o código e o nome gravados na resposta saem como foram gravados. Ausente quando o item não foi respondido.";
}
