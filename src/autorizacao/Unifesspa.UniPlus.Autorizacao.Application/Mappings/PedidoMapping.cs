namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Mapeamento <see cref="PedidoDeConcessao"/> para os DTOs do pedido.</summary>
public static class PedidoMapping
{
    /// <param name="perfil">O perfil do pedido; nulo só se sumiu entre as leituras, e então o nome sai vazio e o prazo não é oferecido.</param>
    public static PedidoDeConcessaoDto ToDto(PedidoDeConcessao p, Perfil? perfil)
    {
        ArgumentNullException.ThrowIfNull(p);

        return new PedidoDeConcessaoDto(
            p.Id, CodigoComRotulo<SituacaoDoPedido>.De(p.Andamento.Situacao), EtapaAtual(p),
            new IdentidadeDto(p.Solicitante.Emissor, p.Solicitante.ContaId, p.Solicitante.Nome),
            Beneficiario(p),
            new PerfilResumoDto(p.PerfilId, perfil?.Nome ?? string.Empty),
            NumeroDaVersao(p, perfil), Escopo(p), p.Declaracao.Justificativa,
            p.Declaracao.ValidaAteSugerida, p.Andamento.ValidaAte, perfil?.AdmitePrazo ?? false,
            p.CriadoEm, p.Andamento.EncerradoEm, p.ConcessaoId,
            Anexos(p), Decisoes(p), TrocasDeVersao(p),
            p.Declaracao.AnexaraDocumento, Termo(p));
    }

    public static PedidoResumoDto ToResumo(PedidoComPerfil item)
    {
        ArgumentNullException.ThrowIfNull(item);

        PedidoDeConcessao p = item.Pedido;

        return new PedidoResumoDto(
            p.Id, CodigoComRotulo<SituacaoDoPedido>.De(p.Andamento.Situacao), EtapaAtual(p),
            Beneficiario(p),
            new PerfilResumoDto(p.PerfilId, item.PerfilNome.Valor),
            item.Versao, Escopo(p), p.Declaracao.Justificativa,
            p.CriadoEm, p.AguardaAceiteDoTermo);
    }

    public static AnexoDoPedidoDto ToDto(AnexoDoPedido a)
    {
        ArgumentNullException.ThrowIfNull(a);

        return new AnexoDoPedidoDto(a.Id, CodigoComRotulo<SituacaoDoAnexo>.De(a.Situacao), a.TamanhoDeclarado, a.EnviadoEm);
    }

    // O número é o da versão que o próprio pedido segue, nunca o da vigente: o pedido encerrado mantém a versão dele.
    private static int? NumeroDaVersao(PedidoDeConcessao p, Perfil? perfil) =>
        perfil?.Versoes.FirstOrDefault(v => v.Id == p.VersaoId)?.Numero;

    private static TrocaDeVersaoDoPedidoDto ToDto(TrocaDeVersaoDoPedido t) =>
        new(t.VersaoAnterior, t.VersaoNova, CodigoComRotulo<DesfechoDaTrocaDeVersao>.De(t.Desfecho), t.Instante);

    private static DecisaoDoPedidoDto ToDto(DecisaoDoPedido d) => new(
        CodigoComRotulo<EtapaDoPedido>.De(d.Etapa),
        new IdentidadeDto(d.DecisorEmissor, d.DecisorContaId, d.DecisorNome),
        CodigoComRotulo<ResultadoDaDecisao>.De(d.Resultado),
        d.Motivo,
        d.Instante,
        d.PermissoesAvaliadas,
        CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter(d.SensibilidadeAvaliada)),
        d.Dispensa);

    private static BeneficiarioDoPedidoDto Beneficiario(PedidoDeConcessao p) => new(
        p.BeneficiarioEmail,
        p.Beneficiario.Identidade is { } b ? new IdentidadeDto(b.Emissor, b.ContaId, b.Nome) : null,
        p.Beneficiario.Nome);

    private static AnexoDoPedidoDto[] Anexos(PedidoDeConcessao p) => [.. p.Anexos.OrderBy(a => a.Id).Select(ToDto)];

    private static DecisaoDoPedidoDto[] Decisoes(PedidoDeConcessao p) =>
        [.. p.Decisoes.OrderBy(d => d.Instante).ThenBy(d => d.PosicaoNoInstante).Select(ToDto)];

    private static TrocaDeVersaoDoPedidoDto[] TrocasDeVersao(PedidoDeConcessao p) =>
        [.. p.TrocasDeVersao.OrderBy(t => t.Instante).ThenBy(t => t.Id).Select(ToDto)];

    private static TermoDoPedidoDto? Termo(PedidoDeConcessao p) =>
        p.Termo is { } termo ? new TermoDoPedidoDto(termo.VersaoId, termo.Numero, termo.Resumo, Aceite(p.AceiteDoTermo)) : null;

    private static AceiteDoTermoDto? Aceite(AceiteDoTermo? a) =>
        a is null ? null : new AceiteDoTermoDto(new IdentidadeDto(a.PessoaEmissor, a.PessoaContaId, a.PessoaNome), a.AceitoEm, a.VersaoDoTermoId, a.Numero, a.Resumo);

    private static EscopoDto Escopo(PedidoDeConcessao p) => new(CodigoComRotulo<TipoDeEscopo>.De(p.Escopo.Tipo), p.UnidadeId);

    private static CodigoComRotulo<EtapaDoPedido>? EtapaAtual(PedidoDeConcessao p) =>
        p.Andamento.EtapaAtual is { } etapa ? CodigoComRotulo<EtapaDoPedido>.De(etapa) : null;
}
