namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="ComposicaoVagasModalidade"/> e o token textual
/// exposto por <c>ModalidadeView.ComposicaoVagas</c> (Configuracao.Contracts,
/// ADR-0056).
/// </summary>
public static class ComposicaoVagasModalidadeCodigo
{
    public const string RestanteDasVagasOfertadas = "RESTANTE_DAS_VAGAS_OFERTADAS";
    public const string DentroDaReservaLegal = "DENTRO_DA_RESERVA_LEGAL";
    public const string RetiradaDeOutraModalidade = "RETIRADA_DE_OUTRA_MODALIDADE";
    public const string SuplementarAoTotal = "SUPLEMENTAR_AO_TOTAL";

    /// <summary>Token não reconhecido mapeia para <see cref="ComposicaoVagasModalidade.Nenhuma"/> (ver <see cref="NaturezaLegalModalidadeCodigo.FromCodigo"/>).</summary>
    /// <summary>
    /// Converte o enum local de volta ao token cross-módulo publicado pelo
    /// catálogo de modalidades — ver <see cref="NaturezaLegalModalidadeCodigo.ToCodigo"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="composicao"/> é o sentinela <see cref="ComposicaoVagasModalidade.Nenhuma"/>.</exception>
    public static string ToCodigo(this ComposicaoVagasModalidade composicao) => composicao switch
    {
        ComposicaoVagasModalidade.RestanteDasVagasOfertadas => RestanteDasVagasOfertadas,
        ComposicaoVagasModalidade.DentroDaReservaLegal => DentroDaReservaLegal,
        ComposicaoVagasModalidade.RetiradaDeOutraModalidade => RetiradaDeOutraModalidade,
        ComposicaoVagasModalidade.SuplementarAoTotal => SuplementarAoTotal,
        ComposicaoVagasModalidade.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(composicao), composicao, "ComposicaoVagasModalidade.Nenhuma é sentinela e não tem token canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(composicao), composicao, "ComposicaoVagasModalidade desconhecida."),
    };

    public static ComposicaoVagasModalidade FromCodigo(string? codigo) => codigo switch
    {
        RestanteDasVagasOfertadas => ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
        DentroDaReservaLegal => ComposicaoVagasModalidade.DentroDaReservaLegal,
        RetiradaDeOutraModalidade => ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
        SuplementarAoTotal => ComposicaoVagasModalidade.SuplementarAoTotal,
        _ => ComposicaoVagasModalidade.Nenhuma,
    };
}
