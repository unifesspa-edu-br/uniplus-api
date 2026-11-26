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
        ["consequenciaDoDescumprimento"] = IndeferimentoDoDocumentoInput.CaminhoDaConsequencia,
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

    /// <summary>A finalidade do código, nula quando o cliente não a declarou ou declarou um código desconhecido.</summary>
    public static FinalidadeFormulario? FinalidadeDoCodigo(string? codigo) => CodigoLidoDoPedido.Ler<FinalidadeFormulario>(codigo);

    /// <summary>A recusa da finalidade declarada que não é código de finalidade; o texto ausente não é recusado.</summary>
    public static IEnumerable<FieldError> RecusaDaFinalidadeDesconhecida(string? codigo) =>
        CodigoLidoDoPedido.TentarLerOpcional(codigo, out FinalidadeFormulario? _)
            ? []
            : FormaDoCabecalho.ValidarFinalidade(null);
}
