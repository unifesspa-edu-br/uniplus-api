namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

/// <summary>
/// Localiza a exigência e o grupo que determina sua repetição na versão da inscrição.
/// </summary>
public static class ExigenciaDaInscricao
{
    public sealed record Localizada(DocumentoExigido Exigencia, string? GrupoRepetido);

    public static Localizada? Localizar(RegrasDeComprovacaoDaVersao regras, Guid exigenciaId)
    {
        ArgumentNullException.ThrowIfNull(regras);

        foreach (RegraDeComprovacao regra in regras.RegrasSemGrupo)
        {
            if (Localizar(regra, exigenciaId, null) is { } encontrada)
            {
                return encontrada;
            }
        }

        return null;
    }

    private static Localizada? Localizar(RegraDeComprovacao regra, Guid exigenciaId, string? grupoRepetido)
    {
        string? repetido = regra.GrupoDoFormularioParaRepeticao ?? grupoRepetido;
        if (regra.Forma == FormaDeComprovacao.Documento)
        {
            return regra.DocumentoExigido is { } documento && documento.Id == exigenciaId ? new Localizada(documento, repetido) : null;
        }

        return LocalizarNosItens(regra.Itens, exigenciaId, repetido);
    }

    private static Localizada? LocalizarNosItens(IEnumerable<RegraDeComprovacao> itens, Guid exigenciaId, string? repetido)
    {
        foreach (RegraDeComprovacao item in itens)
        {
            if (Localizar(item, exigenciaId, repetido) is { } encontrada)
            {
                return encontrada;
            }
        }

        return null;
    }
}
