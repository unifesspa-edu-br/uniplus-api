namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Conversão dos tokens de uma exigência do payload para o vocabulário do domínio.</summary>
internal static class EscritaDaExigencia
{
    // O domínio nomeia o campo da forma da exigência pelo nome plano; no pedido ele vive no objeto aninhado.
    private static readonly Dictionary<string, string> CaminhosNoPedido = new(StringComparer.Ordinal)
    {
        ["aplicabilidade"] = AplicabilidadeDaExigenciaInput.CaminhoDoTipo,
        ["condicoes"] = AplicabilidadeDaExigenciaInput.CaminhoDasCondicoes,
        ["consequenciaIndeferimento"] = IndeferimentoDoDocumentoInput.CaminhoDaConsequencia,
        ["tamanhoMaximoBytes"] = RequisitosDoDocumentoInput.CaminhoDoTamanhoMaximo,
        ["finalidade"] = MomentoDaExigenciaInput.CaminhoDaFinalidade,
    };

    /// <summary>A recusa da forma da exigência no caminho em que o campo aparece no pedido.</summary>
    public static FieldError NoCaminhoDoPedido(FieldError recusa) =>
        recusa.Field is { } campo && CaminhosNoPedido.TryGetValue(campo, out string? caminho)
            ? recusa with { Field = caminho }
            : recusa;

    /// <summary>A aplicabilidade do código, nula quando ausente ou desconhecida: a forma da exigência a recusa.</summary>
    public static Aplicabilidade? AplicabilidadeDoToken(string? token) => CodigoLidoDoPedido.Ler<Aplicabilidade>(token);

    /// <summary>
    /// A finalidade do token, nula quando o cliente não a declarou. O token desconhecido vira o sentinela,
    /// que a forma do documento recusa.
    /// </summary>
    public static FinalidadeFormulario? FinalidadeDoToken(string? token) =>
        token is null ? null : EstruturaFormulario.FinalidadeDoToken(token);
}
