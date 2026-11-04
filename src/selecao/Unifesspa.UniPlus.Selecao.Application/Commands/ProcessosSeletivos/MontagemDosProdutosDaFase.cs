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
    public async Task<Result<ProdutosDaFaseMontados>> MontarAsync(
        FaseCronogramaInput fase, int indice, string faseCodigo, CancellationToken cancellationToken)
    {
        RotuloDoDonoDoProduto dono = RotuloDoDonoDoProduto.DaFase(faseCodigo);
        List<ProdutoDaFase> produtos = [];
        List<FieldError> recusas = [];
        for (int posicao = 0; posicao < fase.Produtos.Count; posicao++)
        {
            string campo = $"fases[{indice}].produtos[{posicao}]";
            Result<ProdutoConferido> conferido = await ConferirAsync(fase.Produtos[posicao], campo, dono, cancellationToken).ConfigureAwait(false);
            if (conferido.IsFailure)
            {
                return Result<ProdutosDaFaseMontados>.ValidationFailure([.. recusas, .. conferido.Errors]);
            }

            switch (conferido.Value)
            {
                case ProdutoConferido.Aceito aceito:
                    produtos.Add(aceito.Produto);
                    break;
                case ProdutoConferido.Recusado recusado:
                    recusas.Add(recusado.Recusa);
                    break;
            }
        }

        return Result<ProdutosDaFaseMontados>.Success(new ProdutosDaFaseMontados(produtos, recusas));
    }

    // A falha é a interrupção; a recusa do produto é um desfecho, e não uma falha.
    private async Task<Result<ProdutoConferido>> ConferirAsync(
        ProdutoDaFaseInput declarado, string campo, RotuloDoDonoDoProduto dono, CancellationToken cancellationToken)
    {
        if (!PapelProdutoFaseCodigo.TentarConverter(declarado.Papel, out PapelProdutoFase? papel))
        {
            return Recusa($"{campo}.papel", ConferenciaDoProdutoDoAto.PapelDesconhecido(declarado.Papel, dono));
        }

        Result<ClassificacaoDoAtoConsultado> classificada = await atos
            .ClassificacaoAsync(declarado.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (classificada.IsFailure)
        {
            return InterrupcaoDaFase.Com<ProdutoConferido>($"{campo}.atoCodigo", classificada.Error!);
        }

        if (classificada.Value is not ClassificacaoDoAtoConsultado.Conhecida conhecida)
        {
            return Recusa($"{campo}.atoCodigo", ConferenciaDoProdutoDoAto.AtoNaoEncontrado(declarado.AtoCodigo, dono));
        }

        return ConferenciaDoProdutoDoAto.RecusaDoPapel(declarado.AtoCodigo, papel, conhecida.Classificacao, dono) is { } recusaDoPapel
            ? Recusa($"{campo}.papel", recusaDoPapel)
            : Result<ProdutoConferido>.Success(new ProdutoConferido.Aceito(ProdutoDaFase.Criar(declarado.AtoCodigo, papel)));
    }

    private static Result<ProdutoConferido> Recusa(string campo, DomainError erro) =>
        Result<ProdutoConferido>.Success(new ProdutoConferido.Recusado(new FieldError(campo, erro)));
}

/// <summary>Os produtos da fase que passaram na conferência e as recusas dos demais.</summary>
internal sealed record ProdutosDaFaseMontados(List<ProdutoDaFase> Produtos, List<FieldError> Recusas)
{
    /// <summary>
    /// Sem todos os produtos íntegros não há o que a fábrica da fase avaliar: chamá-la com a coleção
    /// incompleta reportaria violações inventadas pela própria falha anterior.
    /// </summary>
    public bool Integros => Recusas.Count == 0;
}
