namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// A apresentação de um documento exigido pela inscrição: a unidade lógica que a exigência conta,
/// com um ou mais arquivos — a frente e o verso, por exemplo. Liga-se à inscrição, à identidade da
/// exigência na árvore congelada da versão da inscrição e, quando a exigência se repete por
/// ocorrência de um grupo, à ocorrência. A exigência é guardada só pelo id: não há chave estrangeira
/// para a configuração viva (ADR-0070).
/// </summary>
public sealed class ApresentacaoDeDocumento : EntityBase
{
    public Guid InscricaoId { get; private set; }

    /// <summary>O <c>DocumentoExigido.Id</c> da árvore congelada da versão da inscrição.</summary>
    public Guid ExigenciaId { get; private set; }

    /// <summary>O id da ocorrência do grupo repetível; nulo quando a exigência não se repete.</summary>
    public string? OcorrenciaId { get; private set; }

    private ApresentacaoDeDocumento() { }

    public static ApresentacaoDeDocumento Criar(Guid inscricaoId, Guid exigenciaId, string? ocorrenciaId) => new()
    {
        InscricaoId = inscricaoId,
        ExigenciaId = exigenciaId,
        OcorrenciaId = ocorrenciaId,
    };
}
