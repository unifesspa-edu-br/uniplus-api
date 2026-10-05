namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

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
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, string> pontoResolucaoPorFato = VocabularioDeFatos.PontoResolucaoPorFato(catalogo.Values);
        Dictionary<string, string> membroPorAgregado = VocabularioDeFatos.MembroPorAgregado(catalogo.Values);
        HashSet<string> noProcesso = VocabularioDeFatos.QueOProcessoResolve(processo, catalogo.Values);

        foreach (DocumentoExigido documento in processo.DocumentosExigidos)
        {
            IReadOnlySet<string> camposDaRepeticao = processo.CamposDaRepeticao(documento.Id);
            foreach (string fato in documento.Condicoes.Select(static c => c.Fato).Distinct(StringComparer.Ordinal))
            {
                if (!noProcesso.Contains(fato) && !camposDaRepeticao.Contains(fato))
                {
                    return new DomainError(
                        FatoForaDoProcesso,
                        $"O fato '{fato}', citado pelo gatilho de um documento exigido, não é mais coletado nem derivado pelo processo.");
                }

                if (processo.RecusaDeFaseDoGatilho(fato, documento.ExigidoNaFaseId, documento.Finalidade, pontoResolucaoPorFato, membroPorAgregado) is { } recusa)
                {
                    return recusa;
                }
            }
        }

        return processo.PendenciaDeGrupoQueAlimentaAgregado(membroPorAgregado);
    }
}
