namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Constrói a árvore de exigências a partir das entradas e a define no processo, sem gravar: quem chama
/// fecha a transação. As raízes mantidas seguem como estão, na frente das novas, e a aplicação de um modelo
/// de formulário usa isso para trazer o seu conjunto sem tirar o resto.
/// </summary>
internal static class DefinicaoDosDocumentosExigidos
{
    public static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacaoInformada> entradas,
        IReadOnlyList<NoExigencia> mantidas,
        CadastrosDosDocumentosExigidos cadastros,
        CancellationToken cancellationToken)
    {
        List<NoDeEntradaConferido> conferidos = EscritaDaArvoreDeExigencias.Conferir(entradas, processo.GruposColetados);
        List<RegraDeComprovacaoInformada> folhas = EscritaDaArvoreDeExigencias.FolhasConferidas(conferidos);
        ContextoDaMontagemDasExigencias contexto = await LerContextoAsync(processo, folhas, cadastros, cancellationToken).ConfigureAwait(false);
        Result<List<NoExigencia>> raizes = await new MontagemDaArvoreDeExigencias(new MontagemDoDocumentoExigido(contexto))
            .MontarRaizesAsync(conferidos, ProximaOrdemDaRaiz(mantidas), cancellationToken).ConfigureAwait(false);
        return raizes.IsFailure
            ? Result.ValidationFailure(raizes.Errors)
            : DefinirNoProcesso(processo, mantidas, raizes.Value!, contexto.Fatos);
    }

    private static int ProximaOrdemDaRaiz(IReadOnlyList<NoExigencia> mantidas) =>
        mantidas.Count == 0 ? 0 : mantidas.Max(static r => r.Ordem) + 1;

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo,
        IReadOnlyList<NoExigencia> mantidas,
        List<NoExigencia> raizes,
        FatosDoGatilho? fatos)
    {
        Result vinculoNovo = fatos is null ? Result.Success() : ConferenciaDosVinculosDasExigencias.Conferir(processo, fatos, raizes);
        if (vinculoNovo.IsFailure)
        {
            return vinculoNovo;
        }

        Result definida = processo.DefinirDocumentosExigidos([.. mantidas, .. raizes]);
        return definida.IsFailure ? Result.Failure(definida.Error!) : Result.Success();
    }

    // Os modelos citados pelas folhas são lidos numa consulta só, e só os deste processo: o de outro processo
    // cai na mesma recusa do inexistente. O catálogo de fatos só é lido (I/O cross-módulo) quando alguma
    // folha declara gatilho ou fato comprovado, que também é vínculo novo a conferir.
    private static async Task<ContextoDaMontagemDasExigencias> LerContextoAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacaoInformada> entradas,
        CadastrosDosDocumentosExigidos cadastros,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> idsDosModelos = RegrasDeComprovacaoInformadas.ModelosCitados(entradas);
        IReadOnlyDictionary<Guid, ModeloDeDocumento> modelos = idsDosModelos.Count == 0
            ? new Dictionary<Guid, ModeloDeDocumento>()
            : (await cadastros.Modelos.ListarDoProcessoAsync(processo.Id, idsDosModelos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static m => m.Id);
        bool existeGatilho = RegrasDeComprovacaoInformadas.ExisteGatilho(entradas) || RegrasDeComprovacaoInformadas.ExisteFatoComprovado(entradas);
        FatosDoGatilho? fatos = existeGatilho
            ? await FatosDoGatilho.LerAsync(processo, cadastros.Fatos, cancellationToken).ConfigureAwait(false)
            : null;
        return new ContextoDaMontagemDasExigencias(processo, cadastros.TiposDeDocumento, modelos, fatos);
    }
}
