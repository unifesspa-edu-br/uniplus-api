namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// As partes recusadas das exigências como a conferência as conhece: a exigência pelo nível da lista (<c>documentosExigidos.exigencias</c>)
/// e a regra de comprovação, com os itens dela, pelo nível da lista das regras. A causa aponta o documento pela posição enviada.
/// </summary>
internal static class PartesRecusadasDasExigencias
{
    internal const string ListaDasExigencias = "documentosExigidos.exigencias";
    internal const string ListaDasRegras = "documentosExigidos.regrasDeComprovacao";

    internal static IEnumerable<ParteRecusada> DaExigencia(ExigenciaLida lida) =>
        lida.Recusas.Select(recusa => Parte(
            ListaDasExigencias, lida.Chave, lida.PosicaoNoDocumento, recusa.NoCaminhoDoItem($"{ListaDasExigencias}[{lida.PosicaoNoDocumento}]")));

    internal static ParteRecusada DaRegra(int posicaoDaRaiz, FieldError recusa) => Parte(ListaDasRegras, null, posicaoDaRaiz, recusa);

    private static ParteRecusada Parte(string lista, string? chave, int posicao, FieldError recusa) =>
        new(lista, chave, posicao, recusa with { Field = PonteiroNoDocumento.Converter(recusa.Field, OrigemNoDocumento.Identidade) });
}
