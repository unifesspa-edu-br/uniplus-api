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
        Conferir(regras, new HashSet<string>(grupos.Select(static grupo => grupo.Codigo), StringComparer.Ordinal), "regrasDeComprovacao", RepeticaoDaRegra.SemRepeticao);

    private static List<RegraInformadaConferida> Conferir(IReadOnlyList<RegraDeComprovacaoInformada> regras, IReadOnlySet<string> grupos, string caminho, RepeticaoDaRegra repeticaoDoGrupoQueContem) =>
        [.. regras.Select((regra, indice) => ConferirRegra(regra, grupos, $"{caminho}[{indice}]", indice, repeticaoDoGrupoQueContem))];

    private static RegraInformadaConferida ConferirRegra(RegraDeComprovacaoInformada regra, IReadOnlySet<string> grupos, string caminho, int indice, RepeticaoDaRegra repeticaoDoGrupoQueContem)
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar(regra.Forma, regra.Documento is not null, regra.Itens?.Count ?? 0,
            regra.QuantidadeMinima(), regra.ConsequenciaDoGrupo(), regra.BasesLegaisDoGrupo().Count > 0, DistincaoDe(regra));
        List<FieldError> recusas = [.. declaracao.Recusas.Select(erro => Contextualizar(erro, caminho, indice))];
        recusas.AddRange(FormaDaRegra(regra, declaracao.Forma).Select(erro => erro.NoCaminhoDe(caminho)));
        DomainError? repeticao = ValidadorRepeticaoPorGrupo.RecusarGrupoInexistente(regra.GrupoDoFormularioParaRepeticao, grupos);
        if (repeticao is not null)
        {
            recusas.Add(Contextualizar(new FieldError("grupoDoFormularioParaRepeticao", repeticao), caminho, indice));
        }
        RepeticaoDaRegra estado = repeticaoDoGrupoQueContem.Avancar(regra.GrupoDoFormularioParaRepeticao, repeticao is not null);
        return new(regra, declaracao, caminho, indice, recusas, estado, Conferir(regra.Itens ?? [], grupos, $"{caminho}.itens", estado));
    }

    public static List<RegraDeComprovacaoInformada> RegrasDeDocumentoConferidas(IEnumerable<RegraInformadaConferida> regras) =>
        [.. Percorrer(regras).Where(static r => r.Declaracao.Forma == FormaDeComprovacao.Documento && r.Entrada.Documento is not null)
            .Select(static r => r.Entrada with { Itens = null })];

    public static List<RegraDeComprovacaoInformada> RegrasParaLeituraDeFatos(IEnumerable<RegraInformadaConferida> regras) =>
        [.. Percorrer(regras).Where(static r => r.Declaracao.Forma == FormaDeComprovacao.Documento && r.Entrada.Documento is not null && !r.Repeticao.Recusada)
            .Select(static r => r.Entrada with { Itens = null })];

    private static IEnumerable<RegraInformadaConferida> Percorrer(IEnumerable<RegraInformadaConferida> regras)
    {
        foreach (RegraInformadaConferida regra in regras)
        {
            yield return regra;
            foreach (RegraInformadaConferida item in Percorrer(regra.Itens))
            {
                yield return item;
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

    private static List<FieldError> FormaDaRegra(RegraDeComprovacaoInformada regra, FormaDeComprovacao? forma)
    {
        bool ehRegraDeDocumento = forma == FormaDeComprovacao.Documento;
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
