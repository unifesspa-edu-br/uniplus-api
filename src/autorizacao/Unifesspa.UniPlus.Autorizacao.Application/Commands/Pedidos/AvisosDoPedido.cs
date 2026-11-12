namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using System.Net;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Monta os e-mails que a nova situação do pedido pede. Os handlers só chamam <see cref="AvisarAsync"/> e devolvem o
/// resultado como mensagens de saída, que entram no outbox junto da gravação da transição.
/// </summary>
/// <remarks>
/// Os endereços vêm do pedido (e-mail verificado do token, beneficiário institucional) e das concessões ativas, nunca
/// de texto livre. O corpo traz o perfil, o escopo, a situação e o link: nome de pessoa, justificativa e motivo da
/// recusa ficam no pedido, que só quem tem acesso abre.
/// </remarks>
public sealed class AvisosDoPedido
{
    private readonly IConcessaoRepository _concessoes;
    private readonly ILeitorDeUnidade _unidades;
    private readonly ILinkDoPedido _link;
    private readonly TimeProvider _timeProvider;

    public AvisosDoPedido(
        IConcessaoRepository concessoes, ILeitorDeUnidade unidades, ILinkDoPedido link, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _concessoes = concessoes;
        _unidades = unidades;
        _link = link;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Os avisos da situação em que o pedido ficou. O pedido que espera o documento não avisa ninguém, e o cancelado
    /// que ainda esperava o documento também não: o beneficiário nunca soube dele.
    /// </summary>
    /// <param name="situacaoAnterior">A situação de antes da transição; nula na criação.</param>
    public async Task<IReadOnlyList<EnviarEmail>> AvisarAsync(
        PedidoDeConcessao pedido, string perfilNome, SituacaoDoPedido? situacaoAnterior, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        IReadOnlyList<string> destinatarios = await DestinatariosAsync(pedido, situacaoAnterior, cancellationToken).ConfigureAwait(false);
        if (destinatarios.Count == 0)
        {
            return [];
        }

        string escopo = await EscopoAsync(pedido, cancellationToken).ConfigureAwait(false);

        return Emails(destinatarios, TextosDoAvisoDoPedido.Da(pedido.Situacao), perfilNome, escopo, _link.Para(pedido.Id));
    }

    private static List<EnviarEmail> Emails(
        IReadOnlyList<string> destinatarios, TextoDoAviso texto, string perfilNome, string escopo, string link)
    {
        string corpoEmTexto = CorpoEmTexto(texto, perfilNome, escopo, link);
        string corpoEmHtml = CorpoEmHtml(texto, perfilNome, escopo, link);

        return [.. destinatarios
            .Select(Email.Criar)
            .Where(email => email.IsSuccess)
            .Select(email => EnviarEmail.Para(email.Value!, texto.Assunto, corpoEmTexto, corpoEmHtml, texto.Finalidade))];
    }

    private Task<IReadOnlyList<string>> DestinatariosAsync(
        PedidoDeConcessao pedido, SituacaoDoPedido? situacaoAnterior, CancellationToken cancellationToken) =>
        pedido.Situacao switch
        {
            SituacaoDoPedido.AguardandoBeneficiario => Imediato([pedido.BeneficiarioEmail]),
            SituacaoDoPedido.AguardandoGestor => TitularesAsync(pedido, PerfisDeSistema.GestorDeAcessosId, [], cancellationToken),
            SituacaoDoPedido.AguardandoProtecaoDeDados => TitularesDosRepresentantesAsync(pedido, cancellationToken),
            SituacaoDoPedido.Aprovado or SituacaoDoPedido.Recusado =>
                Imediato(Distintos([pedido.SolicitanteEmail, pedido.BeneficiarioEmail])),
            SituacaoDoPedido.Cancelado when situacaoAnterior != SituacaoDoPedido.AguardandoDocumento =>
                Imediato(DestinatariosDoCancelamento(pedido)),
            _ => Imediato([]),
        };

    private static Task<IReadOnlyList<string>> Imediato(IReadOnlyList<string> destinatarios) => Task.FromResult(destinatarios);

    private Task<IReadOnlyList<string>> TitularesDosRepresentantesAsync(PedidoDeConcessao pedido, CancellationToken cancellationToken)
    {
        Identidade[] aprovadoresDoGestor =
        [
            .. pedido.Decisoes
                .Where(d => d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado)
                .Select(d => d.Decisor),
        ];

        return TitularesAsync(pedido, PerfisDeSistema.RepresentanteDoControladorId, aprovadoresDoGestor, cancellationToken);
    }

    // Quem cancela é o solicitante; só o beneficiário, que já sabia do pedido, precisa saber que ele acabou.
    private static string[] DestinatariosDoCancelamento(PedidoDeConcessao pedido) =>
        pedido.Beneficiario == pedido.Solicitante
            ? []
            : [.. Distintos([pedido.BeneficiarioEmail]).Where(e => !MesmoEndereco(e, pedido.SolicitanteEmail))];

    // Quem decide não pode ser parte do pedido, e o aprovador da etapa do gestor não decide a da proteção de dados:
    // avisar quem não pode agir só enche a caixa de entrada.
    private async Task<IReadOnlyList<string>> TitularesAsync(
        PedidoDeConcessao pedido, Guid perfilId, IReadOnlyCollection<Identidade> excluidos, CancellationToken cancellationToken)
    {
        IReadOnlyList<TitularComEmail> titulares = await _concessoes
            .ListarTitularesAtivosComEmailAsync(perfilId, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return Distintos(titulares
            .Where(t => t.Pessoa != pedido.Solicitante
                && !(pedido.Beneficiario is { } beneficiario && t.Pessoa == beneficiario)
                && !MesmoEndereco(t.Email, pedido.SolicitanteEmail)
                && !MesmoEndereco(t.Email, pedido.BeneficiarioEmail)
                && !excluidos.Contains(t.Pessoa))
            .Select(t => t.Email));
    }

    private static string[] Distintos(IEnumerable<string?> enderecos) =>
        [.. enderecos
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)];

    private static bool MesmoEndereco(string endereco, string? outro) =>
        outro is not null && string.Equals(endereco, outro, StringComparison.OrdinalIgnoreCase);

    private async Task<string> EscopoAsync(PedidoDeConcessao pedido, CancellationToken cancellationToken)
    {
        if (pedido.UnidadeId is not { } unidadeId)
        {
            return "Instituição";
        }

        UnidadeView? unidade = await _unidades.ObterPorIdAsync(unidadeId, cancellationToken).ConfigureAwait(false);

        return unidade?.Sigla ?? "Unidade";
    }

    private static string CorpoEmTexto(TextoDoAviso texto, string perfil, string escopo, string link) =>
        $"{texto.Abertura}\n\nPerfil: {perfil}\nEscopo: {escopo}\nSituação: {texto.Situacao}\n\nAbra o pedido em: {link}";

    private static string CorpoEmHtml(TextoDoAviso texto, string perfil, string escopo, string link)
    {
        string endereco = WebUtility.HtmlEncode(link);

        // Abertura e situação são texto fixo; o nome do perfil e a sigla vêm do cadastro e vão escapados.
        return $"<p>{texto.Abertura}</p>"
            + $"<p>Perfil: <b>{WebUtility.HtmlEncode(perfil)}</b><br>Escopo: <b>{WebUtility.HtmlEncode(escopo)}</b><br>Situação: <b>{texto.Situacao}</b></p>"
            + $"<p><a href=\"{endereco}\">Abrir o pedido</a></p>";
    }
}
