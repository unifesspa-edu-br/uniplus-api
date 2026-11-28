namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Exigencias;
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
        return DefinirNoProcesso(processo, mantidas, montadas, contexto.Fatos);
    }

    private static int ProximaOrdemDasRegras(IReadOnlyList<RegraDeComprovacao> mantidas) =>
        mantidas.Count == 0 ? 0 : mantidas.Max(static r => r.Ordem) + 1;

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo,
        IReadOnlyList<RegraDeComprovacao> mantidas,
        MontagemDasRegrasDeComprovacao.Montadas montadas,
        FatosDoGatilho? fatos)
    {
        List<FieldError> recusasAntesDoDominio = RecusasAntesDoDominio(processo, montadas, fatos);
        return recusasAntesDoDominio.Count == 0
            ? DefinirNoAgregado(processo, mantidas, montadas.Regras)
            : Result.ValidationFailure([.. recusasAntesDoDominio, .. RecusasDoDominio(processo, mantidas, montadas)]);
    }

    // A montagem vem antes porque as demais conferências leem o que ela monta; a pessoa corrige todos os assuntos de uma vez.
    private static List<FieldError> RecusasAntesDoDominio(
        ProcessoSeletivo processo, MontagemDasRegrasDeComprovacao.Montadas montadas, FatosDoGatilho? fatos) =>
        [
            .. montadas.Recusas,
            .. montadas.VinculosDosTipos,
            .. ConferenciaDosVinculosDasExigencias.Conferir(
                processo, fatos, montadas.Regras, CaminhosDasExigencias.NasPosicoes(CaminhosDasExigencias.ListaDasRegrasNoPedido, montadas.PosicoesNoPedido)),
        ];

    private static Result DefinirNoAgregado(ProcessoSeletivo processo, IReadOnlyList<RegraDeComprovacao> mantidas, List<RegraDeComprovacao> regras)
    {
        Result definido = processo.DefinirDocumentosExigidos([.. mantidas, .. regras]);
        return definido.IsSuccess || definido.Errors.Count == 0 || mantidas.Count == 0
            ? definido
            : Result.ValidationFailure(NaPosicaoDoPedido(definido.Errors, mantidas));
    }

    // Só as exigências que montaram entram: a recusada na montagem, e o grupo com item recusado, ficam de fora. As recusas saem sem
    // chamar o método que altera o processo: nada é gravado com recusa pendente.
    private static List<FieldError> RecusasDoDominio(
        ProcessoSeletivo processo, IReadOnlyList<RegraDeComprovacao> mantidas, MontagemDasRegrasDeComprovacao.Montadas montadas)
    {
        IReadOnlyList<string> caminhos =
        [
            .. CaminhosDasExigencias.NaLista(CaminhosDasExigencias.ListaDasRegrasNoPedido, mantidas.Count),
            .. CaminhosDasExigencias.NasPosicoes(CaminhosDasExigencias.ListaDasRegrasNoPedido, montadas.PosicoesNoPedido.Select(posicao => posicao + mantidas.Count)),
        ];
        return NaPosicaoDoPedido(RegrasDasExigencias.RecusasDaDefinicao(processo.Declaracao(), [.. mantidas, .. montadas.Regras], caminhos), mantidas);
    }

    private static List<FieldError> NaPosicaoDoPedido(IEnumerable<FieldError> recusas, IReadOnlyList<RegraDeComprovacao> mantidas) =>
        [.. recusas.Select(erro => PosicaoDasRegrasNovas.SemAsMantidas(erro, mantidas.Count))];

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
