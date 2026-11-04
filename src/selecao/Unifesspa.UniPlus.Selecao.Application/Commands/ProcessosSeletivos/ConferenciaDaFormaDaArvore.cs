namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

/// <summary>
/// Percorre a árvore de entrada em pré-ordem, sem nenhum I/O, confirmando a forma de cada nó antes de
/// resolver qualquer cadastro externo — a validação vence o I/O também na árvore, não só em listas planas
/// (ADR-0125). Em folha confere o documento (aplicabilidade, consequência, tamanho máximo, coerência entre
/// geral e condição) e cada base legal; em grupo, só as bases legais. A construção do grupo fica fora.
/// </summary>
/// <remarks>
/// Devolve as violações do primeiro nó com defeito, acumuladas entre si: acumular entre nós exigiria um
/// caminho posicional sem convenção estabelecida.
/// </remarks>
internal static class ConferenciaDaFormaDaArvore
{
    public static List<FieldError> Conferir(IReadOnlyList<NoExigenciaInput> nos)
    {
        foreach (NoExigenciaInput no in nos)
        {
            List<FieldError> erros = FormaDoNo(no);
            if (erros.Count == 0)
            {
                erros = Conferir(no.Filhos ?? []);
            }

            if (erros.Count > 0)
            {
                return erros;
            }
        }

        return [];
    }

    private static List<FieldError> FormaDoNo(NoExigenciaInput no)
    {
        bool ehFolha = ArvoreDeEntradaDasExigencias.EhFolha(no);
        List<FieldError> erros = ehFolha && no.Documento is { } documento ? FormaDoDocumento(documento) : [];
        IReadOnlyList<BaseLegalInput> basesLegais = ehFolha ? no.Documento?.BasesLegais ?? [] : no.BasesLegais ?? [];
        for (int indice = 0; indice < basesLegais.Count; indice++)
        {
            erros.AddRange(BasesLegaisDoPayload.ComIndice(FormaDaBaseLegal(basesLegais[indice], ehFolha), indice));
        }

        return erros;
    }

    private static List<FieldError> FormaDoDocumento(ItemDocumentoExigidoInput documento) =>
        DocumentoExigido.ValidarFormaBasica(
            LeituraDaExigenciaDoPayload.AplicabilidadeDoToken(documento.Aplicabilidade),
            documento.ConsequenciaIndeferimento,
            documento.TamanhoMaximoBytes,
            documento.Condicoes.Count,
            LeituraDaExigenciaDoPayload.FinalidadeDoToken(documento.Finalidade),
            documento.FatosComprovados);

    private static List<FieldError> FormaDaBaseLegal(BaseLegalInput baseLegal, bool daFolha) =>
        daFolha
            ? DocumentoExigidoBaseLegal.ValidarFormaBasica(
                baseLegal.Referencia, BasesLegaisDoPayload.Abrangencia(baseLegal), BasesLegaisDoPayload.Status(baseLegal))
            : NoExigenciaBaseLegal.ValidarFormaBasica(
                baseLegal.Referencia, BasesLegaisDoPayload.Abrangencia(baseLegal), BasesLegaisDoPayload.Status(baseLegal));
}
