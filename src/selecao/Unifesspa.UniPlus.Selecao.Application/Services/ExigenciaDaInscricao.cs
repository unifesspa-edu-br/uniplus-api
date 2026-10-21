namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

/// <summary>
/// Localiza uma exigência de documento na árvore congelada da versão da inscrição, com o grupo
/// repetível que a faz valer por ocorrência — o próprio nó ou o de um ancestral.
/// </summary>
public static class ExigenciaDaInscricao
{
    public sealed record Localizada(DocumentoExigido Exigencia, string? GrupoRepetido);

    public static Localizada? Localizar(ArvoreExigenciasCongelada arvore, Guid exigenciaId)
    {
        ArgumentNullException.ThrowIfNull(arvore);

        foreach (NoExigencia raiz in arvore.Raizes)
        {
            if (Localizar(raiz, exigenciaId, null) is { } encontrada)
            {
                return encontrada;
            }
        }

        return null;
    }

    private static Localizada? Localizar(NoExigencia no, Guid exigenciaId, string? grupoRepetido)
    {
        string? repetido = no.RepetePorEntidade ?? grupoRepetido;
        if (no.Tipo == TipoNo.Folha)
        {
            return no.DocumentoExigido is { } documento && documento.Id == exigenciaId ? new Localizada(documento, repetido) : null;
        }

        foreach (NoExigencia filho in no.Filhos)
        {
            if (Localizar(filho, exigenciaId, repetido) is { } encontrada)
            {
                return encontrada;
            }
        }

        return null;
    }
}
