namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

/// <summary>Em que ponto está uma fase da versão publicada vigente no instante da retificação (UNI-REQ-0211).</summary>
public enum AndamentoDaFase
{
    /// <summary>A fase ainda não começou: nem a janela abriu, nem houve ato ou registro dela.</summary>
    PorComecar = 1,

    /// <summary>A fase começou e ainda não terminou o que declara.</summary>
    EmAndamento,

    /// <summary>A fase terminou o que declara: o definitivo dela foi publicado, ou a fase seguinte já começou.</summary>
    Concluida,
}
