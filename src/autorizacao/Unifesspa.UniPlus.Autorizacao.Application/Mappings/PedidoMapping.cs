namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Mapeamento <see cref="PedidoDeConcessao"/> para os DTOs do pedido.</summary>
public static class PedidoMapping
{
    public static PedidoDeConcessaoDto ToDto(PedidoDeConcessao p, string perfilNome)
    {
        ArgumentNullException.ThrowIfNull(p);

        return new PedidoDeConcessaoDto(
            p.Id,
            p.Situacao.ToCodigo(),
            p.EtapaAtual?.ToCodigo(),
            new IdentidadeDto(p.SolicitanteEmissor, p.SolicitanteSub, p.SolicitanteNome),
            Beneficiario(p),
            new PerfilResumoDto(p.PerfilId, perfilNome),
            Escopo(p),
            p.Justificativa,
            p.ValidaAteSugerida,
            p.ValidaAte,
            p.CriadoEm,
            p.EncerradoEm,
            p.ConcessaoId,
            [.. p.Anexos.OrderBy(a => a.Id).Select(ToDto)],
            [.. p.Decisoes.OrderBy(d => d.Instante).ThenBy(d => d.Id).Select(ToDto)]);
    }

    public static PedidoResumoDto ToResumo(PedidoComPerfil item)
    {
        ArgumentNullException.ThrowIfNull(item);

        PedidoDeConcessao p = item.Pedido;

        return new PedidoResumoDto(
            p.Id,
            p.Situacao.ToCodigo(),
            p.EtapaAtual?.ToCodigo(),
            Beneficiario(p),
            new PerfilResumoDto(p.PerfilId, item.PerfilNome),
            Escopo(p),
            p.Justificativa,
            p.CriadoEm);
    }

    public static AnexoDoPedidoDto ToDto(AnexoDoPedido a)
    {
        ArgumentNullException.ThrowIfNull(a);

        return new AnexoDoPedidoDto(a.Id, a.Situacao.ToCodigo(), a.TamanhoDeclarado, a.EnviadoEm);
    }

    private static DecisaoDoPedidoDto ToDto(DecisaoDoPedido d) => new(
        d.Etapa.ToCodigo(),
        new IdentidadeDto(d.DecisorEmissor, d.DecisorSub, d.DecisorNome),
        d.Resultado.ToCodigo(),
        d.Motivo,
        d.Instante,
        d.PermissoesAvaliadas,
        d.SensibilidadeAvaliada,
        d.Dispensa);

    private static BeneficiarioDoPedidoDto Beneficiario(PedidoDeConcessao p) => new(
        p.BeneficiarioEmail,
        p.Beneficiario is { } b ? new IdentidadeDto(b.Emissor, b.Sub, b.Nome) : null,
        p.BeneficiarioNome);

    private static EscopoDto Escopo(PedidoDeConcessao p) => new(p.Escopo.Tipo.ToCodigo(), p.UnidadeId);
}
