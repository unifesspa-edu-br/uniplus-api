namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>As regras dos campos textuais do tipo de conceito e dos valores categóricos.</summary>
internal static class CamposDoTipoConceito
{
    public const int CodigoMaxLength = 64;
    public const int NomeMaxLength = 200;
    public const int DescricaoMaxLength = 1000;
    public const int RotuloDoValorMaxLength = 100;

    public static readonly CampoTextual Codigo = new(
        "Código do tipo de conceito", CodigoMaxLength,
        TipoConceitoErrorCodes.CodigoTamanho, TipoConceitoErrorCodes.CodigoCaractereInvalido, FormaDoTexto.UmaLinha);

    public static readonly CampoTextual Nome = new(
        "Nome do tipo de conceito", NomeMaxLength,
        TipoConceitoErrorCodes.NomeTamanho, TipoConceitoErrorCodes.NomeCaractereInvalido, FormaDoTexto.UmaLinha);

    public static readonly CampoTextual Descricao = new(
        "Descrição do tipo de conceito", DescricaoMaxLength,
        TipoConceitoErrorCodes.DescricaoTamanho, TipoConceitoErrorCodes.DescricaoCaractereInvalido, FormaDoTexto.VariasLinhas);

    public static readonly CampoTextual RotuloDoValor = new(
        "Rótulo do valor categórico", RotuloDoValorMaxLength,
        TipoConceitoErrorCodes.ValorCategoricoRotuloInvalido, TipoConceitoErrorCodes.ValorCategoricoRotuloInvalido, FormaDoTexto.Livre);
}
