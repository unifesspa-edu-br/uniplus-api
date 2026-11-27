namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>Conversão dos tokens de uma exigência do payload para o vocabulário do domínio.</summary>
internal static class EscritaDaExigencia
{
    // O domínio nomeia o campo da forma da exigência pelo nome plano; no pedido ele vive no objeto aninhado.
    private static readonly Dictionary<string, string> CaminhosNoPedido = new(StringComparer.Ordinal)
    {
        ["aplicabilidade"] = AplicabilidadeDaExigenciaInput.CaminhoDoTipo,
        ["condicoes"] = AplicabilidadeDaExigenciaInput.CaminhoDasCondicoes,
        ["consequenciaDoDescumprimento"] = IndeferimentoDoDocumentoInput.CaminhoDaConsequencia,
        ["obrigatorio"] = IndeferimentoDoDocumentoInput.CaminhoDoObrigatorio,
        ["abrePrazoParaReenvio"] = IndeferimentoDoDocumentoInput.CaminhoDoPrazoParaReenvio,
        ["tamanhoMaximoBytes"] = RequisitosDoDocumentoInput.CaminhoDoTamanhoMaximo,
    };

    /// <summary>A recusa da forma da exigência no caminho em que o campo aparece no pedido.</summary>
    public static FieldError NoCaminhoDoPedido(FieldError recusa) =>
        recusa.Field is { } campo && CaminhosNoPedido.TryGetValue(campo, out string? caminho)
            ? recusa with { Field = caminho }
            : recusa;

    /// <summary>A aplicabilidade do código, nula quando ausente ou desconhecida: a forma da exigência a recusa.</summary>
    public static Aplicabilidade? AplicabilidadeDoToken(string? token) => CodigoLidoDoPedido.Ler<Aplicabilidade>(token);

    /// <summary>O código do formulário informado, nulo quando ausente ou fora do formato: a forma da exigência já recusou o código que não é de formulário conhecido.</summary>
    public static CodigoDoFormulario? FormularioDoCodigo(string? codigo) => CodigoDoFormulario.Criar(codigo).Value;

    /// <summary>A recusa do código do formulário ausente ou fora do formato; o código é obrigatório em toda exigência.</summary>
    public static IEnumerable<FieldError> RecusaDoCodigoDoFormulario(string? codigo) =>
        CodigoDoFormulario.Criar(codigo) is { IsFailure: true, Error: { } recusa }
            ? [new FieldError(MomentoDaExigenciaInput.CaminhoDoFormulario, recusa)]
            : [];
}
