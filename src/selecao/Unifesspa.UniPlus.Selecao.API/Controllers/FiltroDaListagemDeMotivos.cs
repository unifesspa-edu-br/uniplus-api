namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lê o filtro da listagem de motivos. O tipo chega como texto e é convertido pelo mesmo mapa
/// canônico que a criação usa, e não por binding no enum: ligado ao enum, o parâmetro aceitaria a
/// grafia dos membros em C# e recusaria justamente o código que a API devolve em <c>tipo</c>.
/// </summary>
internal static class FiltroDaListagemDeMotivos
{
    public static bool TentarLer(
        string? tipo,
        string? subclassificacao,
        bool apenasAtivos,
        out FiltroDosMotivos filtro,
        [NotNullWhen(false)] out ProblemDetails? recusa)
    {
        TipoMotivoDeDecisao? tipoFiltrado = string.IsNullOrWhiteSpace(tipo) ? null : TipoMotivoDeDecisaoCodigo.FromCodigo(tipo.Trim());
        string? subclassificacaoFiltrada = string.IsNullOrWhiteSpace(subclassificacao) ? null : subclassificacao.Trim();
        filtro = new FiltroDosMotivos(tipoFiltrado, subclassificacaoFiltrada, apenasAtivos);

        // Nenhum é sentinela de ausência: chega aqui por código desconhecido, e aceitá-lo filtraria
        // por um conjunto sempre vazio, dizendo "não há motivos" em vez de "o filtro é inválido".
        recusa = tipoFiltrado == TipoMotivoDeDecisao.Nenhum
            ? new ProblemDetails
            {
                Title = "Tipo de motivo desconhecido",
                Detail = $"O filtro aceita apenas os códigos {TipoMotivoDeDecisaoCodigo.CodigosEmTexto}.",
                Status = StatusCodes.Status400BadRequest,
            }
            : null;
        return recusa is null;
    }
}
