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
        ContextoDaMontagemDasExigencias contexto = await LerContextoAsync(processo, regrasDeDocumento, EscritaDasRegrasDeComprovacao.RegrasParaLeituraDeFatos(conferidos), mantidas, cadastros, cancellationToken).ConfigureAwait(false);
        MontagemDasRegrasDeComprovacao.Montadas montadas = await new MontagemDasRegrasDeComprovacao(new MontagemDoDocumentoExigido(contexto))
            .MontarRegrasSemGrupoAsync(conferidos, ProximaOrdemDasRegras(mantidas), cancellationToken).ConfigureAwait(false);
        return montadas.Regras.IsFailure
            ? Result.ValidationFailure(montadas.Regras.Errors)
            : DefinirNoProcesso(processo, mantidas, montadas, contexto.Fatos);
    }

    private static int ProximaOrdemDasRegras(IReadOnlyList<RegraDeComprovacao> mantidas) =>
        mantidas.Count == 0 ? 0 : mantidas.Max(static r => r.Ordem) + 1;

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacao> mantidas,
        MontagemDasRegrasDeComprovacao.Montadas montadas,
        FatosDoGatilho? fatos)
    {
        List<RegraDeComprovacao> regras = montadas.Regras.Value!;
        Result vinculosDosDados = fatos is null ? Result.Success() : ConferenciaDosVinculosDasExigencias.Conferir(processo, fatos, regras);
        List<FieldError> recusasDeVinculo = [.. montadas.VinculosDosTipos, .. vinculosDosDados.Errors];
        if (recusasDeVinculo.Count > 0)
        {
            return Result.ValidationFailure(recusasDeVinculo);
        }

        return processo.DefinirDocumentosExigidos([.. mantidas, .. regras]);
    }

    // Somente os modelos do processo e os fatos necessários às regras são consultados.
    private static async Task<ContextoDaMontagemDasExigencias> LerContextoAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacaoInformada> entradas,
        IReadOnlyList<RegraDeComprovacaoInformada> regrasParaFatos,
        IReadOnlyList<RegraDeComprovacao> mantidas,
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
        return new ContextoDaMontagemDasExigencias(processo, cadastros.TiposDeDocumento, modelos, fatos, new ReconhecimentoDasExigencias(processo.DocumentosExigidos, mantidas, entradas));
    }
}
