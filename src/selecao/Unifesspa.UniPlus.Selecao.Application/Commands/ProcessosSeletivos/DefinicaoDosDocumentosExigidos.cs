namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Define as regras de documentos preservando as regras existentes indicadas.</summary>
internal static class DefinicaoDosDocumentosExigidos
{
    public static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacaoInformada> entradas,
        IReadOnlyList<RegraDeComprovacao> mantidas,
        CadastrosDosDocumentosExigidos cadastros,
        CancellationToken cancellationToken)
    {
        List<RegraInformadaConferida> conferidos = EscritaDasRegrasDeComprovacao.Conferir(entradas, processo.GruposColetados);
        List<RegraDeComprovacaoInformada> regrasDeDocumento = EscritaDasRegrasDeComprovacao.RegrasDeDocumentoConferidas(conferidos);
        ContextoDaMontagemDasExigencias contexto = await LerContextoAsync(processo, regrasDeDocumento, EscritaDasRegrasDeComprovacao.RegrasParaLeituraDeFatos(conferidos), cadastros, cancellationToken).ConfigureAwait(false);
        Result<List<RegraDeComprovacao>> regrasMontadas = await new MontagemDasRegrasDeComprovacao(new MontagemDoDocumentoExigido(contexto))
            .MontarRegrasSemGrupoAsync(conferidos, ProximaOrdemDasRegras(mantidas), cancellationToken).ConfigureAwait(false);
        return regrasMontadas.IsFailure
            ? Result.ValidationFailure(regrasMontadas.Errors)
            : DefinirNoProcesso(processo, mantidas, regrasMontadas.Value!, contexto.Fatos);
    }

    private static int ProximaOrdemDasRegras(IReadOnlyList<RegraDeComprovacao> mantidas) =>
        mantidas.Count == 0 ? 0 : mantidas.Max(static r => r.Ordem) + 1;

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacao> mantidas,
        List<RegraDeComprovacao> regrasMontadas,
        FatosDoGatilho? fatos)
    {
        Result vinculoNovo = fatos is null ? Result.Success() : ConferenciaDosVinculosDasExigencias.Conferir(processo, fatos, regrasMontadas);
        if (vinculoNovo.IsFailure)
        {
            return vinculoNovo;
        }

        Result definida = processo.DefinirDocumentosExigidos([.. mantidas, .. regrasMontadas]);
        return definida.IsFailure ? Result.Failure(definida.Error!) : Result.Success();
    }

    // Somente os modelos do processo e os fatos necessários às regras são consultados.
    private static async Task<ContextoDaMontagemDasExigencias> LerContextoAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacaoInformada> entradas,
        IReadOnlyList<RegraDeComprovacaoInformada> regrasParaFatos,
        CadastrosDosDocumentosExigidos cadastros,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> idsDosModelos = RegrasDeComprovacaoInformadas.ModelosCitados(entradas);
        IReadOnlyDictionary<Guid, ModeloDeDocumento> modelos = idsDosModelos.Count == 0
            ? new Dictionary<Guid, ModeloDeDocumento>()
            : (await cadastros.Modelos.ListarDoProcessoAsync(processo.Id, idsDosModelos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static m => m.Id);
        bool existeGatilho = RegrasDeComprovacaoInformadas.ExisteGatilho(regrasParaFatos) || RegrasDeComprovacaoInformadas.ExisteFatoComprovado(entradas);
        FatosDoGatilho? fatos = existeGatilho
            ? await FatosDoGatilho.LerAsync(processo, cadastros.Fatos, cancellationToken).ConfigureAwait(false)
            : null;
        return new ContextoDaMontagemDasExigencias(processo, cadastros.TiposDeDocumento, modelos, fatos);
    }
}
