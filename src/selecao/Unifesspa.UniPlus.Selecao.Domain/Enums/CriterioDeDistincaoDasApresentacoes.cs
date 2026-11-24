namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Critério que distingue as apresentações exigidas de um documento.</summary>
public enum CriterioDeDistincaoDasApresentacoes
{
    [CodigoDeContrato("COMPETENCIA_MENSAL", "Competência mensal", Descricao = "Distingue as apresentações pelo mês a que se referem.")]
    CompetenciaMensal = 1,
    [CodigoDeContrato("EXERCICIO_ANUAL", "Exercício anual", Descricao = "Distingue as apresentações pelo ano a que se referem.")]
    ExercicioAnual = 2,
    [CodigoDeContrato("OCORRENCIA", "Ocorrência", Descricao = "Distingue as apresentações por ocorrência.")]
    Ocorrencia = 3,
}
