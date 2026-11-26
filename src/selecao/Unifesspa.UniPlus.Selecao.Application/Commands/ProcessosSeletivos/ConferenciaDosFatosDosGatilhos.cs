namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// No congelamento, os fatos dos gatilhos de exigência são conferidos de novo contra o processo:
/// cada fato continua coletado, derivado ou calculado pelo sistema — ou é campo do grupo pelo qual
/// a exigência se repete —, e conhecido até a fase da
/// exigência que o cita (UNI-REQ-0077, UNI-REQ-0144); e o grupo que alimenta um agregado citado é
/// obrigatório, ele e o campo (UNI-REQ-0074). Recebe o catálogo inteiro, da mesma
/// leitura única do congelamento: a fase de um derivado depende da fase das suas dependências,
/// que o gatilho não cita.
/// </summary>
internal static class ConferenciaDosFatosDosGatilhos
{
    public const string FatoForaDoProcesso = "PredicadoDnf.FatoNaoColetadoPeloProcesso";

    public static DomainError? Conferir(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo, FatosDasVersoesPublicadas? publicadas)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, string> pontoResolucaoPorFato = VocabularioDeFatos.PontoResolucaoPorFato(catalogo.Values);
        Dictionary<string, string> membroPorAgregado = VocabularioDeFatos.MembroPorAgregado(catalogo.Values);
        HashSet<string> noProcesso = VocabularioDeFatos.QueOProcessoResolve(processo, catalogo.Values);

        return processo.DocumentosExigidos
            .Select(documento => ConferirDocumento(processo, documento, noProcesso, pontoResolucaoPorFato, membroPorAgregado, publicadas))
            .FirstOrDefault(static recusa => recusa is not null)
            ?? PendenciasDosFatos.PendenciaDeGrupoQueAlimentaAgregado(processo.Declaracao(), membroPorAgregado);
    }

    private static DomainError? ConferirDocumento(
        ProcessoSeletivo processo,
        DocumentoExigido documento,
        HashSet<string> noProcesso,
        Dictionary<string, string> pontoResolucaoPorFato,
        Dictionary<string, string> membroPorAgregado,
        FatosDasVersoesPublicadas? publicadas)
    {
        IReadOnlySet<string> camposDaRepeticao = RegrasDasExigencias.CamposDaRepeticao(processo.Declaracao(), documento.Id);
        return documento.Condicoes.Select(static c => c.Fato).Distinct(StringComparer.Ordinal)
            .Select(fato => noProcesso.Contains(fato) || camposDaRepeticao.Contains(fato)
                ? FaseEfetivaDoFato.RecusaDeFaseDoGatilho(processo.Declaracao(), fato, documento.ExigidoNaFaseId, documento.FormularioCodigo, pontoResolucaoPorFato, membroPorAgregado, publicadas)
                : new DomainError(
                    FatoForaDoProcesso,
                    $"O fato '{fato}', citado pelo gatilho de um documento exigido, não é mais coletado nem derivado pelo processo."))
            .FirstOrDefault(static recusa => recusa is not null);
    }
}
