namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Período em que a Unidade existe; sem fim, ela segue vigente. O início é anulável para que a ausência no corpo
/// chegue à conferência, que a recusa como obrigatória.
/// </summary>
public readonly record struct VigenciaDaUnidade(DateOnly? Inicio, DateOnly? Fim)
{
    internal IReadOnlyList<FieldError> Validar() => [.. RecusaDoInicio(), .. RecusaDoFim()];

    private IEnumerable<FieldError> RecusaDoInicio()
    {
        if (Inicio is null)
        {
            yield return new FieldError("vigenciaInicio", new DomainError(
                CodigosDeValidacao.Obrigatorio, "Informe o primeiro dia de vigência da Unidade."));
        }
    }

    // Só compara quando o início foi informado: sem ele não há período a comparar.
    private IEnumerable<FieldError> RecusaDoFim()
    {
        if (Fim < Inicio)
        {
            yield return new FieldError("vigenciaFim", new DomainError(
                UnidadeErrorCodes.VigenciaFimAnteriorAoInicio,
                "Data de encerramento da vigência deve ser igual ou posterior à data de início."));
        }
    }
}
