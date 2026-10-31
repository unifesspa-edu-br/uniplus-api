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

        (string finalidade, string assunto, string abertura) = Texto(pedido.Situacao);
        string escopo = await EscopoAsync(pedido, cancellationToken).ConfigureAwait(false);
        string link = _link.Para(pedido.Id);

        string corpoEmTexto = CorpoEmTexto(abertura, perfilNome, escopo, pedido.Situacao, link);
        string corpoEmHtml = CorpoEmHtml(abertura, perfilNome, escopo, pedido.Situacao, link);

        return [.. destinatarios
            .Select(Email.Criar)
            .Where(email => email.IsSuccess)
            .Select(email => EnviarEmail.Para(email.Value!, assunto, corpoEmTexto, corpoEmHtml, finalidade))];
    }

    private async Task<IReadOnlyList<string>> DestinatariosAsync(
        PedidoDeConcessao pedido, SituacaoDoPedido? situacaoAnterior, CancellationToken cancellationToken)
    {
        switch (pedido.Situacao)
        {
            case SituacaoDoPedido.AguardandoBeneficiario:
                return [pedido.BeneficiarioEmail];

            case SituacaoDoPedido.AguardandoGestor:
                return await TitularesAsync(pedido, PerfisDeSistema.GestorDeAcessosId, [], cancellationToken).ConfigureAwait(false);

            case SituacaoDoPedido.AguardandoProtecaoDeDados:
                Identidade[] aprovadoresDoGestor =
                [
                    .. pedido.Decisoes
                        .Where(d => d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado)
                        .Select(d => d.Decisor),
                ];
                return await TitularesAsync(pedido, PerfisDeSistema.ProtecaoDeDadosId, aprovadoresDoGestor, cancellationToken).ConfigureAwait(false);

            case SituacaoDoPedido.Aprovado:
            case SituacaoDoPedido.Recusado:
                return Distintos([pedido.SolicitanteEmail, pedido.BeneficiarioEmail]);

            case SituacaoDoPedido.Cancelado when situacaoAnterior != SituacaoDoPedido.AguardandoDocumento:
                // Quem cancela é o solicitante; só o beneficiário, que já sabia do pedido, precisa saber que ele acabou.
                if (pedido.Beneficiario == pedido.Solicitante)
                {
                    return [];
                }

                return [.. Distintos([pedido.BeneficiarioEmail]).Where(e => !MesmoEndereco(e, pedido.SolicitanteEmail))];

            default:
                return [];
        }
    }

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

    private static (string Finalidade, string Assunto, string Abertura) Texto(SituacaoDoPedido situacao) =>
        situacao switch
        {
            SituacaoDoPedido.AguardandoBeneficiario => (
                "pedido.aceite_pendente",
                "Pedido de acesso aguarda o seu aceite",
                "Há um pedido de acesso em seu nome que aguarda o seu aceite."),
            SituacaoDoPedido.AguardandoGestor => (
                "pedido.fila_do_gestor",
                "Pedido de acesso aguarda a análise do gestor de acessos",
                "Um pedido de acesso aguarda a análise de adequação funcional, que cabe ao gestor de acessos."),
            SituacaoDoPedido.AguardandoProtecaoDeDados => (
                "pedido.fila_da_protecao_de_dados",
                "Pedido de acesso aguarda a análise da proteção de dados",
                "Um pedido de acesso aguarda a análise de adequação à proteção de dados."),
            SituacaoDoPedido.Aprovado => (
                "pedido.aprovado",
                "Pedido de acesso aprovado",
                "O pedido de acesso foi aprovado e o acesso já está concedido."),
            SituacaoDoPedido.Recusado => (
                "pedido.recusado",
                "Pedido de acesso recusado",
                "O pedido de acesso foi recusado."),
            SituacaoDoPedido.Cancelado => (
                "pedido.cancelado",
                "Pedido de acesso cancelado",
                "O pedido de acesso em seu nome foi cancelado pelo solicitante."),
            _ => throw new InvalidOperationException($"A situação {situacao} não gera aviso."),
        };

    private static string Situacao(SituacaoDoPedido situacao) => situacao switch
    {
        SituacaoDoPedido.AguardandoBeneficiario => "Aguardando o aceite do beneficiário",
        SituacaoDoPedido.AguardandoGestor => "Aguardando o gestor de acessos",
        SituacaoDoPedido.AguardandoProtecaoDeDados => "Aguardando a proteção de dados",
        SituacaoDoPedido.Aprovado => "Aprovado",
        SituacaoDoPedido.Recusado => "Recusado",
        SituacaoDoPedido.Cancelado => "Cancelado",
        _ => situacao.ToString(),
    };

    private static string CorpoEmTexto(string abertura, string perfil, string escopo, SituacaoDoPedido situacao, string link) =>
        $"{abertura}\n\nPerfil: {perfil}\nEscopo: {escopo}\nSituação: {Situacao(situacao)}\n\nAbra o pedido em: {link}";

    private static string CorpoEmHtml(string abertura, string perfil, string escopo, SituacaoDoPedido situacao, string link)
    {
        string endereco = WebUtility.HtmlEncode(link);

        // Abertura e situação são texto fixo; o nome do perfil e a sigla vêm do cadastro e vão escapados.
        return $"<p>{abertura}</p>"
            + $"<p>Perfil: <b>{WebUtility.HtmlEncode(perfil)}</b><br>Escopo: <b>{WebUtility.HtmlEncode(escopo)}</b><br>Situação: <b>{Situacao(situacao)}</b></p>"
            + $"<p><a href=\"{endereco}\">Abrir o pedido</a></p>";
    }
}
