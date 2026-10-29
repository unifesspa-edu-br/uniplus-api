namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atributos do recurso alvo de uma decisão de autorização (ADR-0078): o tipo
/// do recurso, os identificadores de escopo que o situam (unidade proprietária,
/// processo, chamada) e a classificação de sensibilidade do dado que a operação
/// retorna. <b>Não</b> carrega dado pessoal do titular — apenas metadados do
/// recurso.
/// </summary>
public sealed record ResourceContext
{
    /// <summary>Tipo do recurso alvo (ex.: <c>Edital</c>, <c>Inscricao</c>).</summary>
    public string RecursoTipo { get; }

    /// <summary>Unidade organizacional proprietária do recurso. Opcional.</summary>
    public Guid? UnidadeProprietariaId { get; }

    /// <summary>Processo seletivo ao qual o recurso pertence. Opcional.</summary>
    public Guid? ProcessoId { get; }

    /// <summary>Chamada à qual o recurso pertence. Opcional.</summary>
    public Guid? ChamadaId { get; }

    /// <summary>Classificação de sensibilidade do dado retornado.</summary>
    public ClassificacaoDeProtecao Sensibilidade { get; }

    private ResourceContext(
        string recursoTipo,
        ClassificacaoDeProtecao sensibilidade,
        Guid? unidadeProprietariaId,
        Guid? processoId,
        Guid? chamadaId)
    {
        RecursoTipo = recursoTipo;
        Sensibilidade = sensibilidade;
        UnidadeProprietariaId = unidadeProprietariaId;
        ProcessoId = processoId;
        ChamadaId = chamadaId;
    }

    /// <summary>
    /// Constrói um <see cref="ResourceContext"/> validado. Rejeita
    /// <paramref name="recursoTipo"/> em branco e qualquer identificador de
    /// escopo informado como <see cref="Guid.Empty"/> — nulo é "escopo não se
    /// aplica"; um valor informado precisa ser um identificador real (o projeto
    /// usa Guid v7).
    /// </summary>
    public static Result<ResourceContext> From(
        string? recursoTipo,
        ClassificacaoDeProtecao sensibilidade,
        Guid? unidadeProprietariaId = null,
        Guid? processoId = null,
        Guid? chamadaId = null)
    {
        if (string.IsNullOrWhiteSpace(recursoTipo))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextRecursoTipoObrigatorio,
                "Tipo do recurso é obrigatório."));
        }

        if (sensibilidade == ClassificacaoDeProtecao.Nenhuma || !Enum.IsDefined(sensibilidade))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextSensibilidadeInvalida,
                "A sensibilidade do recurso é obrigatória: informe um nível da escala de proteção (publico a sensivel)."));
        }

        if (GuidVazioInformado(unidadeProprietariaId)
            || GuidVazioInformado(processoId)
            || GuidVazioInformado(chamadaId))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextEscopoInvalido,
                "Escopo informado não pode ser Guid.Empty — use um identificador real ou nulo."));
        }

        return Result<ResourceContext>.Success(new ResourceContext(
            recursoTipo.Trim(),
            sensibilidade,
            unidadeProprietariaId,
            processoId,
            chamadaId));
    }

    private static bool GuidVazioInformado(Guid? valor) => valor is { } g && g == Guid.Empty;
}
