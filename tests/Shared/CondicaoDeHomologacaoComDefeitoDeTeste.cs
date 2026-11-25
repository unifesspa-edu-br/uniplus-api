namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Errors;

internal static class CondicaoDeHomologacaoComDefeitoDeTeste
{
    internal const int Edicao = 1997;
    internal const string Motivo = "motivo fictício inválido";
    internal const string Dado = "DADO_FICTICIO";
    internal const string Valor = "opção fictícia";
    internal const string CodigoDaForma = "CondicaoDnf.FormaIncoerenteComOperador";

    internal static (string Codigo, string Campo)[] Causas =>
    [
        (HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida, "edicaoDoExame"),
        (MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido, "motivoDesfavoravelCodigo"),
        (CodigoDaForma, "enquadramento[0][0].valor"),
    ];
}
