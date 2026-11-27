namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê uma regra de comprovação sem os itens, pelas fábricas do domínio: a de documento cita a exigência pela chave, e a de grupo reúne os
/// itens já lidos. A declaração (forma, quantidade, consequência, base legal e distinção) e a composição do grupo se conferem antes, para
/// que as recusas independentes voltem juntas, e a fábrica só é chamada quando nada foi recusado. A exigência que a leitura recusou deixa
/// a regra de documento cortada, sem causa nova; o grupo com item recusado fica sem composição conferida, porque ela depende dos itens.
/// </summary>
internal static class LeituraDaRegraDeComprovacao
{
    private const string CampoDaExigencia = "exigenciaChave";
    private const string CodigoDaCitacao = "RegraDeComprovacao.DocumentoObrigatorio";

    internal static RegraPropriaLida Ler(RegraDeComprovacaoNoDocumento regra, IReadOnlyList<NoLido> itens, DocumentosLidos documentos)
    {
        FieldError[] declaracao = [.. Declaracao(regra).Recusas.Select(static recusa => NoCampoDoDocumento(recusa))];
        bool documental = regra.Forma == FormaDeComprovacao.Documento;
        ParteLida<DocumentoExigido> exigencia = documental ? Exigencia(regra.ExigenciaChave, documentos) : ParteLida<DocumentoExigido>.Ausente;
        ParteLida<IReadOnlyList<BaseLegalDaComprovacao>> bases = documental ? ParteLida<IReadOnlyList<BaseLegalDaComprovacao>>.Ausente : LeituraDasBasesLegais.DoGrupo(regra.BasesLegais);
        FieldError[] recusas = [.. declaracao, .. exigencia.Recusas, .. RecusasDoDocumento(regra, documental), .. Composicao(regra, itens, documental), .. bases.Recusas];

        return recusas.Length > 0 || exigencia.Cortada || itens.Any(static item => item.Regra is null)
            ? new(null, recusas)
            : Criar(regra, itens, exigencia.Valor, bases.Valor);
    }

    private static RegraPropriaLida Criar(
        RegraDeComprovacaoNoDocumento regra, IReadOnlyList<NoLido> itens, DocumentoExigido? exigencia, IReadOnlyList<BaseLegalDaComprovacao>? bases)
    {
        DeclaracaoDaRegraDeComprovacao declaracao = Declaracao(regra);
        int ordem = regra.PosicaoNoDocumento + 1;
        Result<RegraDeComprovacao> criada = exigencia is not null
            ? RegraDeComprovacao.CriarRegraDeDocumento(
                exigencia, ordem, declaracao.QuantidadeMinima, regra.CriterioDeDistincao, regra.DataReferencia, OcorrenciasOuNulo(regra), regra.GrupoDoFormularioParaRepeticao)
            : RegraDeComprovacao.CriarGrupo(
                new(regra.Forma!.Value, declaracao.QuantidadeMinima, declaracao.ConsequenciaDoDescumprimento, regra.AbrePrazoParaReenvio),
                ordem,
                bases!,
                [.. itens.Select(static item => item.Regra!)],
                regra.GrupoDoFormularioParaRepeticao);

        return new(criada.Value, criada.Errors);
    }

    private static DeclaracaoDaRegraDeComprovacao Declaracao(RegraDeComprovacaoNoDocumento regra) =>
        DeclaracaoDaRegraDeComprovacao.Interpretar(
            CodigoDoValorLido.De(regra.Forma),
            regra.ExigenciaChave is not null,
            regra.Itens.Count,
            regra.QuantidadeMinima,
            CodigoDoValorLido.De(regra.ConsequenciaDoDescumprimento),
            regra.BasesLegais.Count > 0,
            new DistincaoDeclarada(regra.CriterioDeDistincao, regra.DataReferencia, OcorrenciasOuNulo(regra)));

    // A lista vazia é a ausência de ocorrências esperadas: a regra que não escolhe o critério por ocorrência não a traz.
    private static IReadOnlyList<string>? OcorrenciasOuNulo(RegraDeComprovacaoNoDocumento regra) =>
        regra.OcorrenciasEsperadas.Count == 0 ? null : regra.OcorrenciasEsperadas;

    private static ParteLida<DocumentoExigido> Exigencia(string? chave, DocumentosLidos documentos) =>
        chave switch
        {
            null => ParteLida<DocumentoExigido>.Ausente,
            _ when documentos.PelaChave.TryGetValue(chave, out DocumentoExigido? exigencia) => ParteLida<DocumentoExigido>.Montada(exigencia),
            _ when documentos.ChavesRecusadas.Contains(chave) => ParteLida<DocumentoExigido>.CortadaSemCausa,
            _ => ParteLida<DocumentoExigido>.Recusada([new(CampoDaExigencia, new DomainError(CodigoDaCitacao, "A regra de documento cita uma exigência que o documento não traz."))]),
        };

    private static IEnumerable<FieldError> RecusasDoDocumento(RegraDeComprovacaoNoDocumento regra, bool documental) =>
        documental && CombinacaoDoGrupo.RecusaDoReenvioEmRegraDeDocumento(regra.AbrePrazoParaReenvio) is { } recusa
            ? [new("abrePrazoParaReenvio", recusa)]
            : [];

    // A composição do grupo depende dos itens; com item recusado ou sem item, a recusa é a dele ou a da declaração.
    private static IEnumerable<FieldError> Composicao(RegraDeComprovacaoNoDocumento regra, IReadOnlyList<NoLido> itens, bool documental) =>
        documental || itens.Count == 0 || itens.Any(static item => item.Regra is null)
            ? []
            : RegraDeComprovacao.ConferirComposicao([.. itens.Select(static item => item.Regra!)], regra.GrupoDoFormularioParaRepeticao);

    // O documento nomeia o campo da exigência citada pela chave, e a declaração o chama de documento.
    private static FieldError NoCampoDoDocumento(FieldError recusa) =>
        recusa.Field == "documento" ? recusa with { Field = CampoDaExigencia } : recusa;
}
