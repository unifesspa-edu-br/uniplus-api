namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

/// <summary>
/// Monta os produtos de uma fase: cada código é resolvido contra o catálogo vigente, ou contra a versão
/// publicada quando o tipo já foi declarado, e o papel só é aceito onde o ato É resultado (ADR-0056). A
/// classificação é intrínseca ao tipo e nunca varia por edital, e os atributos que decidem a âncora do
/// recurso são lidos adiante e descartados, porque evoluem com o cadastro e não são copiados para a fase.
/// </summary>
/// <remarks>
/// Uma coleção mal declarada erra em vários itens ao mesmo tempo, então as recusas de produto são
/// acumuladas. Só a versão publicada ilegível interrompe, e a interrupção leva junto as recusas já
/// acumuladas por esta fase.
/// </remarks>
internal sealed class MontagemDosProdutosDaFase(AtosConsultadosNaEdicao atos)
{
    private static readonly IReadOnlyCollection<(string AtoCodigo, PapelProdutoFase? Papel)> NenhumJaDeclarado = [];

    private readonly ConferenciaDoProdutoDeclarado _conferencia = new(atos);

    public async Task<Result<ProdutosConferidos<ProdutoDaFase>>> MontarAsync(
        FaseCronogramaInput fase, int indice, string faseCodigo, CancellationToken cancellationToken)
    {
        Result<ProdutosConferidos<ProdutoDaFase>> conferidos = await _conferencia
            .ConferirTodosAsync(
                [.. fase.Produtos.Select(static p => (p.AtoCodigo, p.Papel))],
                RotuloDoDonoDoProduto.DaFase(faseCodigo),
                NenhumJaDeclarado,
                ProdutoDaFase.Criar,
                cancellationToken)
            .ConfigureAwait(false);
        return conferidos.IsFailure
            ? Result<ProdutosConferidos<ProdutoDaFase>>.ValidationFailure(NoCaminhoDaFase(conferidos.Errors, indice))
            : Result<ProdutosConferidos<ProdutoDaFase>>.Success(
                conferidos.Value! with { Recusas = NoCaminhoDaFase(conferidos.Value!.Recusas, indice) });
    }

    private static List<FieldError> NoCaminhoDaFase(IEnumerable<FieldError> recusas, int indice) =>
        [.. recusas.Select(recusa => recusa.NoCaminhoDe($"fases[{indice}]"))];
}
