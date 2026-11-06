namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>Período em que a Unidade existe; sem fim, ela segue vigente.</summary>
public readonly record struct VigenciaDaUnidade(DateOnly Inicio, DateOnly? Fim)
{
    internal IReadOnlyList<FieldError> Validar() =>
        Fim < Inicio
            ? [new FieldError("vigenciaFim", new DomainError(
                UnidadeErrorCodes.VigenciaFimAnteriorAoInicio,
                "Data de encerramento da vigência deve ser igual ou posterior à data de início."))]
            : [];
}
