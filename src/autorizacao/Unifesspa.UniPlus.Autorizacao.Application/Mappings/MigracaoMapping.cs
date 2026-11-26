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

        TipoDoEventoDoHistoricoDaConcessao tipo = DoTipo(decisao.Resultado);

        return decisao.Ator == AtorDaMigracao.Humano
            ? EventoDaTrilhaDto.DaPessoa(tipo, decisao.Instante, new IdentidadeDto(decisao.DecisorEmissor!, decisao.DecisorContaId!, decisao.DecisorNome), Detalhe(decisao))
            : EventoDaTrilhaDto.DoSistema(tipo, decisao.Instante, null, Detalhe(decisao));
    }

    private static TipoDoEventoDoHistoricoDaConcessao DoTipo(ResultadoDaMigracao resultado) => resultado switch
    {
        ResultadoDaMigracao.Migrar => TipoDoEventoDoHistoricoDaConcessao.MigracaoAprovada,
        ResultadoDaMigracao.Recusar => TipoDoEventoDoHistoricoDaConcessao.MigracaoRecusada,
        ResultadoDaMigracao.Reter => TipoDoEventoDoHistoricoDaConcessao.RetidaNaMigracao,
        ResultadoDaMigracao.NaoMigrada => TipoDoEventoDoHistoricoDaConcessao.NaoMigrada,
        ResultadoDaMigracao.ForaDaLista => TipoDoEventoDoHistoricoDaConcessao.ForaDaListaDaMigracao,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, null),
    };

    private static string Detalhe(DecisaoDaMigracao decisao) =>
        $"Etapa: {decisao.Etapa.Codigo()}. Revisão: {decisao.RevisaoId}.";
}
