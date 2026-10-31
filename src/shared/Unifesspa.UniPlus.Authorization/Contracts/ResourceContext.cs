namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atributos do resource alvo de uma decisão de autorização (ADR-0078): o tipo
/// do resource, os identificadores de escopo que o situam (unidade proprietária,
/// processo, chamada) e a classificação de sensibilidade do dado que a operação
/// retorna. <b>Não</b> carrega dado pessoal do titular — apenas metadados do
/// resource.
/// </summary>
public sealed record ResourceContext
{
    /// <summary>Tipo do resource alvo (ex.: <c>processo-seletivo</c>, <c>inscricao</c>; ver <see cref="ResourceTipos"/>).</summary>
    public string ResourceTipo { get; }

    /// <summary>Unidade organizacional proprietária do resource. Opcional.</summary>
    public Guid? UnidadeProprietariaId { get; }

    /// <summary>Processo seletivo ao qual o resource pertence. Opcional.</summary>
    public Guid? ProcessoId { get; }

    /// <summary>Chamada à qual o resource pertence. Opcional.</summary>
    public Guid? ChamadaId { get; }

    /// <summary>
    /// Identificador do registro alvo da operação (por exemplo, a inscrição cujo dado é revelado), quando a decisão
    /// recai sobre um único registro. Opcional; é só identificador, nunca conteúdo.
    /// </summary>
    public Guid? ResourceId { get; }

    /// <summary>Classificação de sensibilidade do dado retornado.</summary>
    public ClassificacaoDeProtecao Sensibilidade { get; }

    private ResourceContext(
        string resourceTipo,
        ClassificacaoDeProtecao sensibilidade,
        Guid? unidadeProprietariaId,
        Guid? processoId,
        Guid? chamadaId,
        Guid? resourceId)
    {
        ResourceTipo = resourceTipo;
        Sensibilidade = sensibilidade;
        UnidadeProprietariaId = unidadeProprietariaId;
        ProcessoId = processoId;
        ChamadaId = chamadaId;
        ResourceId = resourceId;
    }

    /// <summary>
    /// Constrói um <see cref="ResourceContext"/> validado. Rejeita
    /// <paramref name="resourceTipo"/> em branco ou fora da convenção de <see cref="ResourceTipos"/> e qualquer identificador de
    /// escopo informado como <see cref="Guid.Empty"/> — nulo é "escopo não se
    /// aplica"; um valor informado precisa ser um identificador real (o projeto
    /// usa Guid v7).
    /// </summary>
    public static Result<ResourceContext> From(
        string? resourceTipo,
        ClassificacaoDeProtecao sensibilidade,
        Guid? unidadeProprietariaId = null,
        Guid? processoId = null,
        Guid? chamadaId = null,
        Guid? resourceId = null)
    {
        if (string.IsNullOrWhiteSpace(resourceTipo))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextResourceTipoObrigatorio,
                "Tipo do resource é obrigatório."));
        }

        if (!ResourceTipos.SegueAConvencao(resourceTipo))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextResourceTipoForaDaConvencao,
                "Tipo do resource deve estar em kebab-case, em minúsculas, sem acento (ex.: tipo-documento)."));
        }

        if (sensibilidade == ClassificacaoDeProtecao.Nenhuma || !Enum.IsDefined(sensibilidade))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextSensibilidadeInvalida,
                "A sensibilidade do resource é obrigatória: informe um nível da escala de proteção (publico a sensivel)."));
        }

        if (GuidVazioInformado(unidadeProprietariaId)
            || GuidVazioInformado(processoId)
            || GuidVazioInformado(chamadaId)
            || GuidVazioInformado(resourceId))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextEscopoInvalido,
                "Escopo informado não pode ser Guid.Empty — use um identificador real ou nulo."));
        }

        return Result<ResourceContext>.Success(new ResourceContext(
            resourceTipo,
            sensibilidade,
            unidadeProprietariaId,
            processoId,
            chamadaId,
            resourceId));
    }

    private static bool GuidVazioInformado(Guid? valor) => valor is { } g && g == Guid.Empty;
}
