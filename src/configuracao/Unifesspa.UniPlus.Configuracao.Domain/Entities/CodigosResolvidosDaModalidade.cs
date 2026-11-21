namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os quatro códigos textuais da modalidade resolvidos para os enums, cada um com a recusa de quando não é
/// do vocabulário. Natureza e composição têm padrão quando ausentes; regra de remanejamento e ação no
/// indeferimento são opcionais.
/// </summary>
internal sealed record CodigosResolvidosDaModalidade(
    NaturezaLegal Natureza,
    ComposicaoVagas Composicao,
    RegraRemanejamento? Regra,
    AcaoQuandoIndeferido? Acao,
    IReadOnlyList<FieldError?> Recusas,
    CodigosReconhecidosDaModalidade Reconhecidos)
{
    private delegate bool Analisador<T>(string? codigo, out T valor);

    public static CodigosResolvidosDaModalidade De(DeclaracaoDaModalidade declaracao)
    {
        (NaturezaLegal natureza, FieldError? recusaNatureza) = ResolverNatureza(declaracao.CodigoDaNaturezaLegal);
        (ComposicaoVagas composicao, FieldError? recusaComposicao) = ResolverComposicao(declaracao.CodigoDaComposicaoDeVagas);
        (RegraRemanejamento? regra, FieldError? recusaRegra) = ResolverRegra(declaracao.CodigoDaRegraDeRemanejamento);
        (AcaoQuandoIndeferido? acao, FieldError? recusaAcao) = ResolverAcao(declaracao.CodigoDaAcaoQuandoIndeferido);

        return new(
            natureza,
            composicao,
            regra,
            acao,
            [recusaNatureza, recusaComposicao, recusaRegra, recusaAcao],
            new(recusaNatureza is null, recusaComposicao is null, recusaRegra is null));
    }

    private static (NaturezaLegal Valor, FieldError? Recusa) ResolverNatureza(string? codigo)
    {
        (NaturezaLegal? valor, FieldError? recusa) = Resolver<NaturezaLegal>(
            codigo, NaturezasLegais.TryAnalisar, "naturezaLegal", ModalidadeErrorCodes.NaturezaInvalida,
            "Natureza legal", NaturezasLegais.TokensCanonicos);
        return (valor ?? NaturezaLegal.Ampla, recusa);
    }

    private static (ComposicaoVagas Valor, FieldError? Recusa) ResolverComposicao(string? codigo)
    {
        (ComposicaoVagas? valor, FieldError? recusa) = Resolver<ComposicaoVagas>(
            codigo, ComposicoesVagas.TryAnalisar, "composicaoVagas", ModalidadeErrorCodes.ComposicaoVagasInvalida,
            "Composição de vagas", ComposicoesVagas.TokensCanonicos);
        return (valor ?? ComposicaoVagas.RestanteDasVagasOfertadas, recusa);
    }

    private static (RegraRemanejamento? Valor, FieldError? Recusa) ResolverRegra(string? codigo) =>
        Resolver<RegraRemanejamento>(
            codigo, RegrasRemanejamento.TryAnalisar, "regraRemanejamento", ModalidadeErrorCodes.RegraRemanejamentoInvalida,
            "Regra de remanejamento", RegrasRemanejamento.TokensCanonicos);

    private static (AcaoQuandoIndeferido? Valor, FieldError? Recusa) ResolverAcao(string? codigo) =>
        Resolver<AcaoQuandoIndeferido>(
            codigo, AcoesQuandoIndeferido.TryAnalisar, "acaoQuandoIndeferido", ModalidadeErrorCodes.AcaoIndeferimentoInvalida,
            "Ação quando indeferido", AcoesQuandoIndeferido.TokensCanonicos);

    private static (T? Valor, FieldError? Recusa) Resolver<T>(
        string? codigo, Analisador<T> analisar, string campo, string codigoDaRecusa, string rotulo, IEnumerable<string> codigos)
        where T : struct =>
        string.IsNullOrWhiteSpace(codigo) ? (null, null)
        : analisar(codigo, out T valor) ? (valor, null)
        : (null, new FieldError(campo, new DomainError(
            codigoDaRecusa, $"{rotulo} deve ser uma de: {string.Join(", ", codigos)}.")));
}
