namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O recorte das concessões que valem na instituição, num só lugar para quem conta ou avisa seus titulares.</summary>
internal static class ConcessoesVigentesDaInstituicao
{
    /// <summary>As concessões não revogadas, no escopo da instituição, cuja vigência não venceu em <paramref name="agora"/>.</summary>
    public static IQueryable<Concessao> VigentesDaInstituicao(this IQueryable<Concessao> concessoes, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessoes);

        return concessoes.Where(c => c.EscopoTipo == TipoDeEscopo.Instituicao
            && c.RevogadaEm == null
            && (c.ValidaAte == null || c.ValidaAte > agora));
    }
}
