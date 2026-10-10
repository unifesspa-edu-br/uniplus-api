namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// O comprovante de uma submissão da <see cref="Inscricao"/> (issue #1858, §4.1 do plano da
/// #1855): cópia do que foi enviado, SEM nenhum número de documento, com o hash de integridade
/// (SHA-256 do conteúdo canônico) que prova que o comprovante não mudou — qualquer um o recalcula;
/// não é um código de autenticação.
/// </summary>
/// <remarks>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: append-only, NÃO herda
/// <see cref="Kernel.Domain.Entities.EntityBase"/> e não carrega soft-delete. Uma correção gera um
/// comprovante NOVO; o anterior nunca muda.
/// </remarks>
public sealed class ComprovanteInscricao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid InscricaoId { get; private init; }

    public string NumeroInscricao { get; private init; } = string.Empty;

    public DateTimeOffset Instante { get; private init; }

    public string HashIntegridade { get; private init; } = string.Empty;

    public Guid? OfertaCursoId { get; private init; }

    /// <summary>O que o candidato informou, no momento da submissão — sem números de documento.</summary>
    public IReadOnlyDictionary<string, JsonElement> Respostas { get; private init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>O que o sistema derivou das respostas nesta submissão.</summary>
    public IReadOnlyDictionary<string, JsonElement> Derivados { get; private init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>As ocorrências de grupo repetível no momento da submissão — mesma exclusão de documento que <see cref="Respostas"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos { get; private init; } =
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal);

    public IReadOnlyList<AceiteDeTermo> Aceites { get; private init; } = [];

    // Construtor de materialização do EF Core.
    private ComprovanteInscricao() { }

    public static ComprovanteInscricao Emitir(
        Guid inscricaoId,
        string numeroInscricao,
        DateTimeOffset instante,
        string hashIntegridade,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroInscricao);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashIntegridade);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(derivados);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);

        return new ComprovanteInscricao
        {
            InscricaoId = inscricaoId,
            NumeroInscricao = numeroInscricao,
            Instante = instante,
            HashIntegridade = hashIntegridade,
            Grupos = grupos,
            OfertaCursoId = ofertaCursoId,
            Respostas = respostas,
            Derivados = derivados,
            Aceites = aceites,
        };
    }
}
