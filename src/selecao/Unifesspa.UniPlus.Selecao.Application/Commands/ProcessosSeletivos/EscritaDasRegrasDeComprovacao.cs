namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Confere as regras e reúne as recusas antes de consultar cadastros externos.</summary>
internal static class EscritaDasRegrasDeComprovacao
{
    public static List<RegraInformadaConferida> Conferir(IReadOnlyList<RegraDeComprovacaoInformada> regras, IEnumerable<GrupoColetado> grupos) =>
        Conferir(regras, new HashSet<string>(grupos.Select(static grupo => grupo.Codigo), StringComparer.Ordinal), "regrasDeComprovacao");

    private static List<RegraInformadaConferida> Conferir(IReadOnlyList<RegraDeComprovacaoInformada> regras, IReadOnlySet<string> grupos, string caminho) =>
        [.. regras.Select((regra, indice) => ConferirRegra(regra, grupos, $"{caminho}[{indice}]", indice))];

    private static RegraInformadaConferida ConferirRegra(RegraDeComprovacaoInformada regra, IReadOnlySet<string> grupos, string caminho, int indice)
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar(regra.Forma, regra.Documento is not null, regra.Itens?.Count ?? 0,
            regra.QuantidadeMinima(), regra.ConsequenciaDoGrupo(), regra.BasesLegaisDoGrupo().Count > 0, DistincaoDe(regra));
        List<FieldError> recusas = [.. declaracao.Recusas.Select(erro => Contextualizar(erro, caminho, indice))];
        recusas.AddRange(FormaDaRegra(regra).Select(erro => erro.NoCaminhoDe(caminho)));
        if (ValidadorRepeticaoPorGrupo.RecusarGrupoInexistente(regra.GrupoDoFormularioParaRepeticao, grupos) is { } repeticao)
        {
            recusas.Add(Contextualizar(new FieldError("grupoDoFormularioParaRepeticao", repeticao), caminho, indice));
        }
        return new(regra, declaracao, caminho, indice, recusas, Conferir(regra.Itens ?? [], grupos, $"{caminho}.itens"));
    }

    public static List<RegraDeComprovacaoInformada> RegrasDeDocumentoConferidas(IEnumerable<RegraInformadaConferida> regras) =>
        [.. regras.SelectMany(regra => RegrasDeDocumentoDaRegra(regra, repeticaoRecusada: false))];

    private static IEnumerable<RegraDeComprovacaoInformada> RegrasDeDocumentoDaRegra(RegraInformadaConferida regra, bool repeticaoRecusada)
    {
        bool bloqueada = repeticaoRecusada || RegraInformadaConferida.RepeticaoRecusada(regra);
        if (regra.Declaracao.Forma == FormaDeComprovacao.Documento && regra.Recusas.Count == 0
            && (!bloqueada || regra.Entrada.Documento?.Condicoes().Count == 0))
        {
            yield return regra.Entrada with { Itens = null };
        }
        foreach (RegraInformadaConferida item in regra.Itens)
        {
            foreach (RegraDeComprovacaoInformada regraDeDocumento in RegrasDeDocumentoDaRegra(item, bloqueada))
            {
                yield return regraDeDocumento;
            }
        }
    }

    private static FieldError Contextualizar(FieldError erro, string caminho, int indice) =>
        (erro with { Field = CampoDaDeclaracao(erro.Field) }).NoCaminhoDe(caminho) with
        {
            Error = erro.Error with { Message = $"No item {indice + 1} da lista: {erro.Error.Message}" },
        };

    private static string? CampoDaDeclaracao(string? campo) => campo switch
    {
        "quantidadeMinima" => "cardinalidade.quantidadeMinima",
        "criterioDeDistincao" => "cardinalidade.distincao.criterioDeDistincao",
        "dataReferencia" => "cardinalidade.distincao.dataReferencia",
        "ocorrenciasEsperadas" => "cardinalidade.distincao.ocorrenciasEsperadas",
        "consequencia" => "indeferimento.consequencia",
        "basesLegais" => "indeferimento.basesLegais",
        _ => campo,
    };

    private static List<FieldError> FormaDaRegra(RegraDeComprovacaoInformada regra)
    {
        bool ehRegraDeDocumento = RegrasDeComprovacaoInformadas.EhRegraDeDocumento(regra);
        List<FieldError> erros = ehRegraDeDocumento && regra.Documento is { } documento
            ? FormaDoDocumento(documento).Select(erro => erro.NoCaminhoDe("documento")).ToList() : [];
        IReadOnlyList<BaseLegalInput> basesLegais = ehRegraDeDocumento ? regra.Documento?.BasesLegais() ?? [] : regra.BasesLegaisDoGrupo();
        for (int indice = 0; indice < basesLegais.Count; indice++)
        {
            IEnumerable<FieldError> recusas = EscritaDasBasesLegais.ComIndice(FormaDaBaseLegal(basesLegais[indice], ehRegraDeDocumento), indice);
            erros.AddRange(ehRegraDeDocumento ? recusas.Select(erro => erro.NoCaminhoDe("documento")) : recusas);
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

    private static List<FieldError> FormaDaBaseLegal(BaseLegalInput baseLegal, bool doDocumento) =>
        doDocumento
            ? DocumentoExigidoBaseLegal.ValidarFormaBasica(
                baseLegal.Referencia, EscritaDasBasesLegais.Abrangencia(baseLegal), EscritaDasBasesLegais.Situacao(baseLegal))
            : BaseLegalDaComprovacao.ValidarFormaBasica(
                baseLegal.Referencia, EscritaDasBasesLegais.Abrangencia(baseLegal), EscritaDasBasesLegais.Situacao(baseLegal));
    private static DistincaoDeclarada DistincaoDe(RegraDeComprovacaoInformada regra) =>
        DistincaoDeclarada.Interpretar(regra.Distincao()?.CriterioDeDistincao, regra.Distincao()?.DataReferencia, regra.Distincao()?.OcorrenciasEsperadas);

}
