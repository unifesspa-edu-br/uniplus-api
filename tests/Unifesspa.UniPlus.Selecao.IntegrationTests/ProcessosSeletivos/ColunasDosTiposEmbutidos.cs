namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

internal static class ColunasDosTiposEmbutidos
{
    private static readonly Dictionary<string, (Type Tipo, string[] Propriedades, bool Precisao)> Colunas = new(StringComparer.Ordinal)
    {
        [nameof(LimitesDoEnvelope.RegraCodigo)] = (typeof(ReferenciaRegra), [nameof(ReferenciaRegra.Codigo)], false),
        [nameof(LimitesDoEnvelope.RegraVersao)] = (typeof(ReferenciaRegra), [nameof(ReferenciaRegra.Versao)], false),
        [nameof(LimitesDoEnvelope.CensoReferencia)] = (typeof(ReferenciaReservaDemograficaSnapshot), [nameof(ReferenciaReservaDemograficaSnapshot.CensoReferencia)], false),
        [nameof(LimitesDoEnvelope.GrupoAreaEnemCodigo)] = (typeof(GrupoAreaEnemSnapshot), [nameof(GrupoAreaEnemSnapshot.Codigo)], false),
        [nameof(LimitesDoEnvelope.GrupoAreaEnemRotulo)] = (typeof(GrupoAreaEnemSnapshot), [nameof(GrupoAreaEnemSnapshot.Rotulo)], false),
        [nameof(LimitesDoEnvelope.TipoEtapaCodigo)] = (typeof(TipoEtapaSnapshot), [nameof(TipoEtapaSnapshot.Codigo)], false),
        [nameof(LimitesDoEnvelope.TipoEtapaNome)] = (typeof(TipoEtapaSnapshot), [nameof(TipoEtapaSnapshot.Nome)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraSigla)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.Sigla)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraCodigo)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.Codigo)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraIdentificadorNoEnderecoWeb)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.IdentificadorNoEnderecoWeb)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraNome)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.Nome)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraTipo)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.Tipo)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraCidadeCodigoIbge)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.CidadeCodigoIbge)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraCidadeNome)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.CidadeNome)], false),
        [nameof(LimitesDoEnvelope.UnidadeAdministradoraCidadeUf)] = (typeof(UnidadeAdministradoraSnapshot), [nameof(UnidadeAdministradoraSnapshot.CidadeUf)], false),
        [nameof(LimitesDoEnvelope.PrecisaoPercentual)] = (typeof(ReferenciaReservaDemograficaSnapshot),
            [nameof(ReferenciaReservaDemograficaSnapshot.PpiPercentual), nameof(ReferenciaReservaDemograficaSnapshot.QuilombolaPercentual),
             nameof(ReferenciaReservaDemograficaSnapshot.PcdPercentual)], true),
    };

    internal static (Type Tipo, string[] Propriedades, bool Precisao) Obter(string nome) => Colunas[nome];
}
