namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="ComposicaoVagasModalidade"/> e o token textual
/// exposto por <c>ModalidadeView.ComposicaoVagas</c> (Configuracao.Contracts,
/// ADR-0056).
/// </summary>
public static class ComposicaoVagasModalidadeCodigo
{
    public const string ResidualDoVo = "RESIDUAL_DO_VO";
    public const string DentroDoVr = "DENTRO_DO_VR";
    public const string RetiraDe = "RETIRA_DE";
    public const string SuplementarAoTotal = "SUPLEMENTAR_AO_TOTAL";

    /// <summary>Token não reconhecido mapeia para <see cref="ComposicaoVagasModalidade.Nenhuma"/> (ver <see cref="NaturezaLegalModalidadeCodigo.FromCodigo"/>).</summary>
    public static ComposicaoVagasModalidade FromCodigo(string? codigo) => codigo switch
    {
        ResidualDoVo => ComposicaoVagasModalidade.ResidualDoVo,
        DentroDoVr => ComposicaoVagasModalidade.DentroDoVr,
        RetiraDe => ComposicaoVagasModalidade.RetiraDe,
        SuplementarAoTotal => ComposicaoVagasModalidade.SuplementarAoTotal,
        _ => ComposicaoVagasModalidade.Nenhuma,
    };
}
