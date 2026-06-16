namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Requisito de permissão que o ponto de decisão único avalia (ADR-0078): a
/// permissão pedida e os metadados que condicionam a decisão — sensibilidade do
/// dado, base legal padrão, exigência de multifator e de dupla aprovação, os
/// campos de contexto obrigatórios e as verificações contextuais adicionais.
/// Este tipo é apenas o <b>shape</b> que a decisão recebe; os <b>valores</b>
/// vêm do catálogo declarativo de permissões (story irmã).
/// </summary>
/// <remarks>
/// <see cref="VerificacoesDeContexto"/> traz <b>somente</b> os checks
/// contextuais adicionais (fase, estado do recurso, escopo de auditoria, base
/// legal, equipe, atribuição documental, conformidade legal) — a seleção de
/// grant, o multifator (<see cref="RequerMfa"/>) e a dupla aprovação
/// (<see cref="RequerDuplaAprovacao"/>) são derivados pelo serviço de decisão,
/// não repetidos aqui.
/// </remarks>
public sealed record PermissionRequirement
{
    /// <summary>Código da permissão exigida, do catálogo.</summary>
    public string Permissao { get; }

    /// <summary>Sensibilidade do dado que a operação retorna.</summary>
    public Sensibilidade Sensibilidade { get; }

    /// <summary>Base legal padrão da permissão. Vazia quando o dado é público.</summary>
    public string BaseLegalPadrao { get; }

    /// <summary>A permissão exige multifator satisfeito.</summary>
    public bool RequerMfa { get; }

    /// <summary>A permissão exige dupla aprovação válida.</summary>
    public bool RequerDuplaAprovacao { get; }

    /// <summary>Campos do <see cref="ResourceContext"/> que esta permissão exige presentes.</summary>
    public IReadOnlyList<string> EscopoContextoObrigatorio { get; }

    /// <summary>Verificações contextuais adicionais que a decisão deve orquestrar.</summary>
    public IReadOnlyList<string> VerificacoesDeContexto { get; }

    private PermissionRequirement(
        string permissao,
        Sensibilidade sensibilidade,
        string baseLegalPadrao,
        bool requerMfa,
        bool requerDuplaAprovacao,
        IReadOnlyList<string> escopoContextoObrigatorio,
        IReadOnlyList<string> verificacoesDeContexto)
    {
        Permissao = permissao;
        Sensibilidade = sensibilidade;
        BaseLegalPadrao = baseLegalPadrao;
        RequerMfa = requerMfa;
        RequerDuplaAprovacao = requerDuplaAprovacao;
        EscopoContextoObrigatorio = escopoContextoObrigatorio;
        VerificacoesDeContexto = verificacoesDeContexto;
    }

    /// <summary>
    /// Constrói um <see cref="PermissionRequirement"/> validado. Rejeita
    /// <paramref name="permissao"/> em branco. <paramref name="baseLegalPadrao"/>
    /// nula vira vazia (permitido para dado público). As coleções recebem cópia
    /// defensiva imutável (nulas viram vazias).
    /// </summary>
    public static Result<PermissionRequirement> From(
        string? permissao,
        Sensibilidade sensibilidade,
        string? baseLegalPadrao = null,
        bool requerMfa = false,
        bool requerDuplaAprovacao = false,
        IEnumerable<string>? escopoContextoObrigatorio = null,
        IEnumerable<string>? verificacoesDeContexto = null)
    {
        if (string.IsNullOrWhiteSpace(permissao))
        {
            return Result<PermissionRequirement>.Failure(new DomainError(
                AuthorizationErrorCodes.PermissionRequirementPermissaoObrigatoria,
                "Código da permissão é obrigatório."));
        }

        return Result<PermissionRequirement>.Success(new PermissionRequirement(
            permissao.Trim(),
            sensibilidade,
            baseLegalPadrao ?? string.Empty,
            requerMfa,
            requerDuplaAprovacao,
            ColecoesSomenteLeitura.Lista(escopoContextoObrigatorio),
            ColecoesSomenteLeitura.Lista(verificacoesDeContexto)));
    }

    // Igualdade por valor (CA-01): o Equals sintetizado pelo record compararia as
    // coleções por referência, tornando contratos de conteúdo idêntico desiguais.
    // Compara as listas por sequência; o hash acompanha em ordem.

    /// <inheritdoc />
    public bool Equals(PermissionRequirement? other) =>
        other is not null
        && Permissao == other.Permissao
        && Sensibilidade == other.Sensibilidade
        && BaseLegalPadrao == other.BaseLegalPadrao
        && RequerMfa == other.RequerMfa
        && RequerDuplaAprovacao == other.RequerDuplaAprovacao
        && EscopoContextoObrigatorio.SequenceEqual(other.EscopoContextoObrigatorio)
        && VerificacoesDeContexto.SequenceEqual(other.VerificacoesDeContexto);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Permissao);
        hash.Add(Sensibilidade);
        hash.Add(BaseLegalPadrao);
        hash.Add(RequerMfa);
        hash.Add(RequerDuplaAprovacao);
        foreach (string escopo in EscopoContextoObrigatorio)
        {
            hash.Add(escopo);
        }

        foreach (string verificacao in VerificacoesDeContexto)
        {
            hash.Add(verificacao);
        }

        return hash.ToHashCode();
    }
}
