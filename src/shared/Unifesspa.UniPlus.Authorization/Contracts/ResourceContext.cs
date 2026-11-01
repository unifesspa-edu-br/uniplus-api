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

    /// <summary>
    /// Inscrição a que o acesso se refere, quando o resource é a própria inscrição ou um item dela (um documento, por
    /// exemplo). É o que permite consultar tudo o que foi acessado de uma inscrição.
    /// </summary>
    public Guid? InscricaoId { get; }

    /// <summary>
    /// Código do fato (campo) que a operação alcança dentro do registro alvo, quando ela recai sobre um único campo —
    /// a revelação de um identificador. É o código do catálogo, nunca o valor.
    /// </summary>
    public string? FatoCodigo { get; }

    /// <summary>Ocorrência do fato, quando o campo está num grupo que se repete; nula fora de grupo.</summary>
    public int? Ocorrencia { get; }

    /// <summary>Classificação de sensibilidade do dado retornado.</summary>
    public ClassificacaoDeProtecao Sensibilidade { get; }

    private ResourceContext(
        string resourceTipo,
        ClassificacaoDeProtecao sensibilidade,
        Guid? unidadeProprietariaId,
        Guid? processoId,
        Guid? chamadaId,
        Guid? resourceId,
        string? fatoCodigo,
        int? ocorrencia,
        Guid? inscricaoId)
    {
        ResourceTipo = resourceTipo;
        Sensibilidade = sensibilidade;
        UnidadeProprietariaId = unidadeProprietariaId;
        ProcessoId = processoId;
        ChamadaId = chamadaId;
        ResourceId = resourceId;
        FatoCodigo = fatoCodigo;
        Ocorrencia = ocorrencia;
        InscricaoId = inscricaoId;
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
        Guid? resourceId = null,
        string? fatoCodigo = null,
        int? ocorrencia = null,
        Guid? inscricaoId = null)
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
            || GuidVazioInformado(resourceId)
            || GuidVazioInformado(inscricaoId))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextEscopoInvalido,
                "Escopo informado não pode ser Guid.Empty — use um identificador real ou nulo."));
        }

        if (fatoCodigo is not null && string.IsNullOrWhiteSpace(fatoCodigo))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextFatoInvalido,
                "O código do fato, quando informado, não pode ser vazio."));
        }

        if (ocorrencia is < 0 || (ocorrencia is not null && fatoCodigo is null))
        {
            return Result<ResourceContext>.Failure(new DomainError(
                AuthorizationErrorCodes.ResourceContextOcorrenciaInvalida,
                "A ocorrência exige o código do fato e não pode ser negativa."));
        }

        return Result<ResourceContext>.Success(new ResourceContext(
            resourceTipo,
            sensibilidade,
            unidadeProprietariaId,
            processoId,
            chamadaId,
            resourceId,
            fatoCodigo,
            ocorrencia,
            inscricaoId));
    }

    private static bool GuidVazioInformado(Guid? valor) => valor is { } g && g == Guid.Empty;
}
