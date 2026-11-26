namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

internal static class CertamesDoFiltro
{
    private const string EscapeDoLike = "\\";

    internal static IQueryable<CertameDivulgado> Filtrados(
        this IQueryable<CertameDivulgado> certames, FiltroDosCertamesPublicados filtro, DateTimeOffset instanteUtc) =>
        certames.FiltradosSemSituacao(filtro).NaSituacao(filtro.Situacao, instanteUtc);

    internal static IQueryable<CertameDivulgado> FiltradosSemSituacao(
        this IQueryable<CertameDivulgado> certames, FiltroDosCertamesPublicados filtro)
    {
        IQueryable<CertameDivulgado> porModalidade = filtro.Modalidade is { Length: > 0 } modalidade
            ? certames.ComModalidade(modalidade) : certames;
        string? termo = NormalizacaoTextual.PrepararTermoDeBusca(filtro.Busca);
        return termo is null ? porModalidade : porModalidade.ComBusca(termo);
    }

    /// <remarks>Predicados traduzíveis da regra de SituacaoDaVitrine.Classificar, executada fora do banco.</remarks>
    private static IQueryable<CertameDivulgado> NaSituacao(
        this IQueryable<CertameDivulgado> certames, SituacaoDoCertame? situacao, DateTimeOffset instanteUtc)
    {
        DateTimeOffset limiar = instanteUtc + SituacaoDaVitrine.LimiarDosUltimosDias;
        return situacao switch
        {
            SituacaoDoCertame.EmBreve => certames.Where(c => c.InscricoesAte >= instanteUtc && c.InscricoesDe > instanteUtc),
            SituacaoDoCertame.InscricoesAbertas => certames.Where(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= limiar),
            SituacaoDoCertame.UltimosDias => certames.Where(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= instanteUtc && c.InscricoesAte < limiar),
            SituacaoDoCertame.Encerradas => certames.Where(c => c.InscricoesAte < instanteUtc),
            _ => certames,
        };
    }

    private static IQueryable<CertameDivulgado> ComModalidade(this IQueryable<CertameDivulgado> certames, string modalidade) =>
        certames.Where(c => c.ModalidadesOfertadas.Contains(modalidade));

    private static IQueryable<CertameDivulgado> ComBusca(this IQueryable<CertameDivulgado> certames, string termo)
    {
        string padrao = "%" + termo + "%";
        return certames.Where(c => EF.Functions.ILike(
            EF.Property<string>(c, CertameDivulgadoConfiguration.NomeOrdenacaoPropriedade), padrao, EscapeDoLike)
            || (c.Numero != null && EF.Functions.ILike(FuncoesDaPlataforma.NormalizarParaComparacao(c.Numero), padrao, EscapeDoLike)));
    }
}
