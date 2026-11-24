namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Enums;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>Projeta o papel da publicação prevista, que falta quando o ato não é um resultado.</summary>
internal static class ProjecaoDoPapelDaPublicacao
{
    internal static CodigoComRotulo<PapelDaPublicacaoPrevista>? De(PapelDaPublicacaoPrevista? papel) =>
        papel is { } declarado ? CodigoComRotulo<PapelDaPublicacaoPrevista>.De(declarado) : null;
}
