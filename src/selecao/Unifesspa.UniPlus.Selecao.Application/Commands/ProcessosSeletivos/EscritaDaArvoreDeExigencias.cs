namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Percorre a árvore de entrada em pré-ordem, sem nenhum I/O, confirmando a forma de cada nó antes de
/// resolver qualquer cadastro externo (ADR-0125). Confere a declaração de cada nó, o documento da folha
/// e as bases legais; acumula as recusas com os caminhos das posições enviadas.
/// </summary>
internal static class EscritaDaArvoreDeExigencias
{
    public static List<NoDeEntradaConferido> Conferir(IReadOnlyList<NoExigenciaInput> nos, IEnumerable<GrupoColetado> grupos) =>
        Conferir(nos, new HashSet<string>(grupos.Select(static grupo => grupo.Codigo), StringComparer.Ordinal), "nos");

    private static List<NoDeEntradaConferido> Conferir(IReadOnlyList<NoExigenciaInput> nos, IReadOnlySet<string> grupos, string caminho) =>
        [.. nos.Select((no, indice) => ConferirNo(no, grupos, $"{caminho}[{indice}]", indice))];

    private static NoDeEntradaConferido ConferirNo(NoExigenciaInput no, IReadOnlySet<string> grupos, string caminho, int indice)
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar(no.Tipo, no.Documento is not null, no.Filhos?.Count ?? 0,
            no.QuantidadeMinima(), no.ConsequenciaDoGrupo(), no.BasesLegaisDoGrupo().Count > 0, DistincaoDe(no));
        List<FieldError> recusas = [.. declaracao.Recusas.Select(erro => Contextualizar(erro, caminho, indice))];
        recusas.AddRange(FormaDoNo(no).Select(erro => erro.NoCaminhoDe(caminho)));
        if (ValidadorRepeticaoPorGrupo.RecusarGrupoInexistente(no.RepetePorEntidade, grupos) is { } repeticao)
        {
            recusas.Add(Contextualizar(new FieldError("repetePorEntidade", repeticao), caminho, indice));
        }
        return new(no, declaracao, caminho, indice, recusas, Conferir(no.Filhos ?? [], grupos, $"{caminho}.filhos"));
    }

    public static List<NoExigenciaInput> FolhasConferidas(IEnumerable<NoDeEntradaConferido> nos) =>
        [.. nos.SelectMany(no => FolhasConferidasDoNo(no, repeticaoRecusada: false))];

    private static IEnumerable<NoExigenciaInput> FolhasConferidasDoNo(NoDeEntradaConferido no, bool repeticaoRecusada)
    {
        bool bloqueada = repeticaoRecusada || NoDeEntradaConferido.RepeticaoRecusada(no);
        if (no.Declaracao.Tipo == TipoNo.Folha && no.Recusas.Count == 0
            && (!bloqueada || no.Entrada.Documento?.Condicoes().Count == 0))
        {
            yield return no.Entrada with { Filhos = null };
        }
        foreach (NoDeEntradaConferido filho in no.Filhos)
        {
            foreach (NoExigenciaInput folha in FolhasConferidasDoNo(filho, bloqueada))
            {
                yield return folha;
            }
        }
    }

    private static FieldError Contextualizar(FieldError erro, string caminho, int indice) =>
        erro.NoCaminhoDe(caminho) with
        {
            Error = erro.Error with { Message = $"No item {indice + 1} da lista: {erro.Error.Message}" },
        };

    private static List<FieldError> FormaDoNo(NoExigenciaInput no)
    {
        bool ehFolha = ArvoreDeEntradaDasExigencias.EhFolha(no);
        List<FieldError> erros = ehFolha && no.Documento is { } documento
            ? FormaDoDocumento(documento).Select(erro => erro.NoCaminhoDe("documento")).ToList() : [];
        IReadOnlyList<BaseLegalInput> basesLegais = ehFolha ? no.Documento?.BasesLegais() ?? [] : no.BasesLegaisDoGrupo();
        for (int indice = 0; indice < basesLegais.Count; indice++)
        {
            IEnumerable<FieldError> recusas = EscritaDasBasesLegais.ComIndice(FormaDaBaseLegal(basesLegais[indice], ehFolha), indice);
            erros.AddRange(ehFolha ? recusas.Select(erro => erro.NoCaminhoDe("documento")) : recusas);
        }

        return erros;
    }

    private static List<FieldError> FormaDoDocumento(ItemDocumentoExigidoInput documento) =>
        DocumentoExigido.ValidarFormaBasica(
            EscritaDaExigencia.AplicabilidadeDoToken(documento.Aplicabilidade()),
            documento.Consequencia(),
            documento.TamanhoMaximoBytes(),
            documento.Condicoes().Count,
            EscritaDaExigencia.FinalidadeDoToken(documento.Finalidade()),
            documento.FatosComprovados).Select(EscritaDaExigencia.NoCaminhoDoPedido).ToList();

    private static List<FieldError> FormaDaBaseLegal(BaseLegalInput baseLegal, bool daFolha) =>
        daFolha
            ? DocumentoExigidoBaseLegal.ValidarFormaBasica(
                baseLegal.Referencia, EscritaDasBasesLegais.Abrangencia(baseLegal), EscritaDasBasesLegais.Status(baseLegal))
            : BaseLegalDaComprovacao.ValidarFormaBasica(
                baseLegal.Referencia, EscritaDasBasesLegais.Abrangencia(baseLegal), EscritaDasBasesLegais.Status(baseLegal));
    private static DistincaoDeclarada DistincaoDe(NoExigenciaInput no) =>
        DistincaoDeclarada.Interpretar(no.Distincao()?.Chave, no.Distincao()?.DataReferencia, no.Distincao()?.OcorrenciasEsperadas);

}
