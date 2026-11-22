namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
            codigo, "naturezaLegal", ModalidadeErrorCodes.NaturezaInvalida, "Natureza legal");
        return (valor ?? NaturezaLegal.Ampla, recusa);
    }

    private static (ComposicaoVagas Valor, FieldError? Recusa) ResolverComposicao(string? codigo)
    {
        (ComposicaoVagas? valor, FieldError? recusa) = Resolver<ComposicaoVagas>(
            codigo, "composicaoVagas", ModalidadeErrorCodes.ComposicaoVagasInvalida, "Composição de vagas");
        return (valor ?? ComposicaoVagas.RestanteDasVagasOfertadas, recusa);
    }

    private static (RegraRemanejamento? Valor, FieldError? Recusa) ResolverRegra(string? codigo) =>
        Resolver<RegraRemanejamento>(
            codigo, "remanejamento.regra", ModalidadeErrorCodes.RegraRemanejamentoInvalida, "Regra de remanejamento");

    private static (AcaoQuandoIndeferido? Valor, FieldError? Recusa) ResolverAcao(string? codigo) =>
        Resolver<AcaoQuandoIndeferido>(
            codigo, "acaoQuandoIndeferido", ModalidadeErrorCodes.AcaoIndeferimentoInvalida, "Ação quando indeferido");

    private static (T? Valor, FieldError? Recusa) Resolver<T>(string? codigo, string campo, string codigoDaRecusa, string rotulo)
        where T : struct, Enum =>
        codigo is null ? (null, null)
        : CatalogoDe<T>.TentarObter(codigo, out T valor) ? (valor, null)
        : (null, new FieldError(campo, new DomainError(
            codigoDaRecusa,
            $"{rotulo} deve ser uma de: {string.Join(", ", CatalogoDe<T>.Todos().Select(static declarado => declarado.Codigo))}.")));
}
