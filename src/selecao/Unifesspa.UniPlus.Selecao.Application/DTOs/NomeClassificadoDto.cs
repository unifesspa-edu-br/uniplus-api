namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O nome exibido de uma inscrição, como item classificado: a classificação que as versões do processo congelaram para o
/// nome decide se ele sai, e não a classe de um campo fixo do DTO (ADR-0153).
/// </summary>
public sealed record NomeClassificadoDto : ItemClassificado
{
    /// <summary>Cria o nome com a classificação e a base legal agregadas das versões do processo.</summary>
    public NomeClassificadoDto(ClassificacaoDeProtecao classificacao, string? baseLegal)
        : base(NaturezaDoItem.Dado, classificacao, baseLegal)
    {
    }

    /// <summary>O nome social, quando o candidato o declarou; senão o nome civil.</summary>
    public required string Valor { get; init; }
}
