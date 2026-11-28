namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Por que os parâmetros de um recurso foram recusados. O código de erro NÃO é montado aqui: ele é literal em cada
/// entidade, senão a cobertura do registro de erros não consegue enxergá-lo.
/// </summary>
public enum MotivoDaRecusaDosParametros
{
    PrazoNaoPositivo,
    PrazoPrecisaoInvalida,
    PrazoEmDiasCorridos,
    PrazoSemUnidadeDeclaravel,
    PrazoEmFracaoDeDiaUtil,
    EfeitoSuspensivoNaoDeclarado,
    GuardaDaVagaNaoDeclaravel,
    PrazoSegundaInstanciaNaoDeclarado,
    PrazoSegundaInstanciaSemInstanciaSuperior,
    NaoGuardaComInstanciaSuperior,
    InstanciaSuperiorNaoDeclarada,
}

/// <summary>
/// A recusa de um parâmetro do recurso: o motivo, que vira o código de erro da entidade, o campo recusado, relativo ao
/// recurso (<c>prazoValor</c>, <c>prazoSegundaInstanciaUnidade</c>, <c>efeitoSuspensivo</c>, <c>guardaDaVaga</c>), e a
/// mensagem já redigida, que nunca repete o valor recebido.
/// </summary>
public sealed record RecusaDosParametrosDoRecurso(MotivoDaRecusaDosParametros Motivo, string Campo, string Mensagem)
{
    /// <summary>
    /// O código genérico de validação quando o motivo é de formato ou de campo obrigatório ausente, que não pertence a uma
    /// entidade; nos demais, vazio, e o código é o literal da entidade.
    /// </summary>
    internal string? CodigoDeValidacao => Motivo switch
    {
        MotivoDaRecusaDosParametros.PrazoPrecisaoInvalida => CodigosDeValidacao.Formato,
        MotivoDaRecusaDosParametros.InstanciaSuperiorNaoDeclarada => CodigosDeValidacao.Obrigatorio,
        _ => null,
    };
}
