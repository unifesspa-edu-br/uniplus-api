namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os quatro tokens textuais da modalidade resolvidos para os enums, cada um com a recusa de quando não é
/// do vocabulário. Natureza e composição têm padrão quando ausentes; regra de remanejamento e ação no
/// indeferimento são opcionais.
/// </summary>
internal sealed record TokensResolvidosDaModalidade(
    NaturezaLegal Natureza,
    ComposicaoVagas Composicao,
    RegraRemanejamento? Regra,
    AcaoQuandoIndeferido? Acao,
    IReadOnlyList<FieldError?> Recusas,
    TokensReconhecidosDaModalidade Reconhecidos)
{
    private delegate bool Analisador<T>(string? token, out T valor);

    public static TokensResolvidosDaModalidade De(DeclaracaoDaModalidade declaracao)
    {
        (NaturezaLegal natureza, FieldError? recusaNatureza) = ResolverNatureza(declaracao.NaturezaLegalToken);
        (ComposicaoVagas composicao, FieldError? recusaComposicao) = ResolverComposicao(declaracao.ComposicaoVagasToken);
        (RegraRemanejamento? regra, FieldError? recusaRegra) = ResolverRegra(declaracao.RegraRemanejamentoToken);
        (AcaoQuandoIndeferido? acao, FieldError? recusaAcao) = ResolverAcao(declaracao.AcaoQuandoIndeferidoToken);

        return new(
            natureza,
            composicao,
            regra,
            acao,
            [recusaNatureza, recusaComposicao, recusaRegra, recusaAcao],
            new(recusaNatureza is null, recusaComposicao is null, recusaRegra is null));
    }

    private static (NaturezaLegal Valor, FieldError? Recusa) ResolverNatureza(string? token)
    {
        (NaturezaLegal? valor, FieldError? recusa) = Resolver<NaturezaLegal>(
            token, NaturezasLegais.TryAnalisar, "naturezaLegal", ModalidadeErrorCodes.NaturezaInvalida,
            "Natureza legal", NaturezasLegais.TokensCanonicos);
        return (valor ?? NaturezaLegal.Ampla, recusa);
    }

    private static (ComposicaoVagas Valor, FieldError? Recusa) ResolverComposicao(string? token)
    {
        (ComposicaoVagas? valor, FieldError? recusa) = Resolver<ComposicaoVagas>(
            token, ComposicoesVagas.TryAnalisar, "composicaoVagas", ModalidadeErrorCodes.ComposicaoVagasInvalida,
            "Composição de vagas", ComposicoesVagas.TokensCanonicos);
        return (valor ?? ComposicaoVagas.RestanteDasVagasOfertadas, recusa);
    }

    private static (RegraRemanejamento? Valor, FieldError? Recusa) ResolverRegra(string? token) =>
        Resolver<RegraRemanejamento>(
            token, RegrasRemanejamento.TryAnalisar, "regraRemanejamento", ModalidadeErrorCodes.RegraRemanejamentoInvalida,
            "Regra de remanejamento", RegrasRemanejamento.TokensCanonicos);

    private static (AcaoQuandoIndeferido? Valor, FieldError? Recusa) ResolverAcao(string? token) =>
        Resolver<AcaoQuandoIndeferido>(
            token, AcoesQuandoIndeferido.TryAnalisar, "acaoQuandoIndeferido", ModalidadeErrorCodes.AcaoIndeferimentoInvalida,
            "Ação quando indeferido", AcoesQuandoIndeferido.TokensCanonicos);

    private static (T? Valor, FieldError? Recusa) Resolver<T>(
        string? token, Analisador<T> analisar, string campo, string codigoDaRecusa, string rotulo, IEnumerable<string> tokens)
        where T : struct =>
        string.IsNullOrWhiteSpace(token) ? (null, null)
        : analisar(token, out T valor) ? (valor, null)
        : (null, new FieldError(campo, new DomainError(
            codigoDaRecusa, $"{rotulo} deve ser uma de: {string.Join(", ", tokens)}.")));
}
