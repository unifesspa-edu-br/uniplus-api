namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

/// <summary>
/// O índice único de <c>inscricoes</c> (ADR-0121, issue #1858) — único constraint desta tabela,
/// então não há tradução por nome de constraint: qualquer violação 23505 aqui é a mesma corrida
/// (dois "iniciar" do mesmo CPF no mesmo processo).
/// </summary>
internal static class InscricaoConstraintViolation
{
    public const string ProcessoCpfConstraint = "ux_inscricoes_processo_cpf_indice";
}
