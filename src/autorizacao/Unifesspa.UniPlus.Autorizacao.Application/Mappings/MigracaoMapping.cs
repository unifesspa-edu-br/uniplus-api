namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>A decisão da revisão das concessões como evento da trilha da concessão.</summary>
public static class MigracaoMapping
{
    public static EventoDaTrilhaDto ToEvento(DecisaoDaMigracao decisao)
    {
        ArgumentNullException.ThrowIfNull(decisao);

        bool humano = decisao.Ator == AtorDaMigracao.Humano;
        IdentidadeDto? autor = humano ? new IdentidadeDto(decisao.DecisorEmissor!, decisao.DecisorContaId!, decisao.DecisorNome) : null;

        return new EventoDaTrilhaDto(
            $"migracao_{decisao.Resultado.Codigo().ToLowerInvariant()}",
            decisao.Instante,
            autor,
            Detalhe(decisao),
            decisao.Ator.Codigo());
    }

    private static string Detalhe(DecisaoDaMigracao decisao) =>
        $"Etapa: {decisao.Etapa.Codigo()}. Revisão: {decisao.RevisaoId}.";
}
