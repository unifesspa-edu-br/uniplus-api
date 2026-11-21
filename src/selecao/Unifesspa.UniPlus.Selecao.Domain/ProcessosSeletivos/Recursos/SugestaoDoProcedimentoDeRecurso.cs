namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a janela que declara o procedimento diz sobre como a nota nasce: a forma de avaliar da etapa e se ela é a nota do ENEM.</summary>
/// <param name="FormaDeAvaliacao">A forma de avaliar da etapa dona da janela; nula na janela de fase e na etapa sem avaliação declarada.</param>
/// <param name="NotaDoEnem">A etapa dona da janela declara a nota do ENEM.</param>
public readonly record struct OrigemDaNotaDaJanela(FormaDeAvaliacao? FormaDeAvaliacao, bool NotaDoEnem)
{
    /// <summary>A janela de fase: não tem etapa, logo não tem forma de avaliar.</summary>
    public static OrigemDaNotaDaJanela DaFase { get; } = new(null, false);
}

/// <summary>
/// O procedimento que a plataforma sugere a quem publica, derivado da forma de avaliar da janela. É só uma sugestão de
/// leitura: quem publica troca à vontade, e o que vale é o declarado e congelado, que esta função nunca altera.
/// </summary>
public static class SugestaoDoProcedimentoDeRecurso
{
    /// <summary>O código do procedimento sugerido, ou nulo quando a janela não dá base para sugerir.</summary>
    public static string? Para(TipoMotivoDeDecisao tipoDeDecisao, AlvoDoProcedimentoDeRecurso? alvo, OrigemDaNotaDaJanela origem) =>
        (tipoDeDecisao, alvo?.Tipo) switch
        {
            (TipoMotivoDeDecisao.GabaritoDaProva or TipoMotivoDeDecisao.NotaDaEtapa, _) => null,
            (_, TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem) => RegraProcedimentoRecursoCodigo.Recalculo,
            (TipoMotivoDeDecisao.Classificacao, _) => RegraProcedimentoRecursoCodigo.Recalculo,
            (_, TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao) => DaForma(origem),
            _ => null,
        };

    private static string? DaForma(OrigemDaNotaDaJanela origem) => origem switch
    {
        { NotaDoEnem: true } => RegraProcedimentoRecursoCodigo.Recalculo,
        { FormaDeAvaliacao: FormaDeAvaliacao.Humana } => RegraProcedimentoRecursoCodigo.Recorrecao,
        { FormaDeAvaliacao: FormaDeAvaliacao.Objetiva } => RegraProcedimentoRecursoCodigo.Recalculo,
        _ => null,
    };
}
