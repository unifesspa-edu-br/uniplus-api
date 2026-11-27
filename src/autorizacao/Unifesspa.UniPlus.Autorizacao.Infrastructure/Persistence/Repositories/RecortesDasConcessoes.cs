namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Os recortes das concessões que valem, num só lugar para quem conta, avisa ou consulta seus titulares.</summary>
internal static class RecortesDasConcessoes
{
    /// <summary>
    /// As concessões não revogadas e cuja vigência, se houver, não venceu em <paramref name="agora"/>; a que vence
    /// exatamente em <paramref name="agora"/> já venceu. É <see cref="Concessao.EstaAtiva"/> em forma que o banco traduz.
    /// </summary>
    public static IQueryable<Concessao> Vigentes(this IQueryable<Concessao> concessoes, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessoes);

        return concessoes.Where(c => c.RevogadaEm == null && (c.ValidaAte == null || c.ValidaAte > agora));
    }

    /// <summary>As concessões vigentes em <paramref name="agora"/> no escopo da instituição.</summary>
    public static IQueryable<Concessao> VigentesDaInstituicao(this IQueryable<Concessao> concessoes, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessoes);

        return concessoes.Where(c => c.EscopoTipo == TipoDeEscopo.Instituicao).Vigentes(agora);
    }
}
