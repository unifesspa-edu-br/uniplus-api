namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>A decisão da janela de migração como evento da trilha da concessão.</summary>
public static class MigracaoMapping
{
    public static EventoDaTrilhaDto ToEvento(DecisaoDaMigracao decisao)
    {
        ArgumentNullException.ThrowIfNull(decisao);

        bool humano = decisao.Ator == AtorDaMigracao.Humano;
        IdentidadeDto? autor = humano ? new IdentidadeDto(decisao.DecisorEmissor!, decisao.DecisorSub!, decisao.DecisorNome) : null;

        return new EventoDaTrilhaDto(
            $"migracao_{decisao.Resultado.ToCodigo()}",
            decisao.Instante,
            autor,
            Detalhe(decisao),
            decisao.Ator.ToCodigo());
    }

    private static string Detalhe(DecisaoDaMigracao decisao)
    {
        string dispensa = decisao.Dispensa is null ? string.Empty : $" Dispensa: {decisao.Dispensa}.";

        return $"Etapa: {decisao.Etapa.ToCodigo()}. Janela: {decisao.JanelaId}.{dispensa}";
    }
}
