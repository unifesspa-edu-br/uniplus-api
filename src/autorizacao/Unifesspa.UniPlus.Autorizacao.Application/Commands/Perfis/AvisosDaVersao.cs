namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Os avisos da governança de versões do perfil: a remoção, o cancelamento da pendente por contenção e o descarte das
/// aprovações. Cada um devolve os e-mails para o handler entregá-los ao outbox da transação que gravou o fato. Nenhum
/// leva motivo, nome de pessoa nem e-mail.
/// </summary>
public sealed class AvisosDaVersao
{
    // Os gestores de acessos que mantêm perfis e os Representantes do controlador: quem a contenção avisa.
    private static readonly GrupoDaGovernanca[] DaContencao = [GrupoDaGovernanca.Manutencao, GrupoDaGovernanca.RepresentanteDoControlador];

    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;
    private readonly CatalogoDaAplicacao _catalogo;

    public AvisosDaVersao(
        EmissorDeAvisos emissor, GruposDaGovernanca grupos, ILinkDaGovernanca link, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(catalogo);

        _emissor = emissor;
        _grupos = grupos;
        _link = link;
        _catalogo = catalogo;
    }

    /// <summary>
    /// A remoção avisa os gestores de acessos que mantêm perfis e os Representantes do controlador, de toda permissão
    /// removida, menos quem removeu (UNI-REQ-0359).
    /// </summary>
    public async Task<IReadOnlyList<EnviarEmail>> DaRemocaoAsync(
        Perfil perfil, TrocaDeVigencia troca, Identidade quemRemoveu, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(troca);
        ArgumentNullException.ThrowIfNull(quemRemoveu);

        ModeloDeAviso modelo = Modelo(TipoDeAviso.PermissoesRemovidas, perfil, troca.Nova, troca.Removidas, _link.DoPerfil(perfil.Id));
        List<DestinatarioDoAviso> destinatarios = await _grupos.ListarAsync(DaContencao, cancellationToken).ConfigureAwait(false);

        return await EmitirAsync(modelo, troca.Nova.Id, destinatarios, [quemRemoveu], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A pendente cancelada pela remoção avisa quem a criou, para submeter de novo, e os demais gestores de acessos e os
    /// Representantes do controlador, menos quem removeu (UNI-REQ-0359).
    /// </summary>
    public async Task<IReadOnlyList<EnviarEmail>> DoCancelamentoPorContencaoAsync(
        Perfil perfil, VersaoDoPerfil cancelada, Identidade quemRemoveu, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(cancelada);
        ArgumentNullException.ThrowIfNull(quemRemoveu);

        ModeloDeAviso modelo = Modelo(TipoDeAviso.VersaoCanceladaPelaRemocao, perfil, cancelada, [], _link.DaVersao(perfil.Id, cancelada.Id));
        DestinatarioDoAviso? criador = await IndividualAsync(
            cancelada.CriadaPor, cancelada.CriadaPorEmail, GrupoDaGovernanca.Manutencao, cancellationToken).ConfigureAwait(false);
        List<DestinatarioDoAviso> grupos = await _grupos.ListarAsync(DaContencao, cancellationToken).ConfigureAwait(false);

        return await EmitirAsync(modelo, cancelada.Id, [.. Lista(criador), .. grupos], [quemRemoveu], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>As aprovações descartadas avisam quem as deu.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> DasAprovacoesDescartadasAsync(
        Perfil perfil, VersaoDoPerfil versao, DesfechoDaVersao desfecho, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(desfecho);

        ModeloDeAviso modelo = Modelo(TipoDeAviso.AprovacoesDescartadas, perfil, versao, [], _link.DaVersao(perfil.Id, versao.Id));
        List<DestinatarioDoAviso> decisores = await DecisoresAsync(desfecho.Descartadas, cancellationToken).ConfigureAwait(false);

        return await EmitirAsync(modelo, desfecho.Recalculo!.Id, decisores, [], cancellationToken).ConfigureAwait(false);
    }

    private static DestinatarioDoAviso[] Lista(DestinatarioDoAviso? destinatario) => destinatario is null ? [] : [destinatario];

    private ModeloDeAviso Modelo(
        TipoDeAviso tipo, Perfil perfil, VersaoDoPerfil versao, IEnumerable<string> permissoes, string link) =>
        new(tipo, perfil.Nome, versao.Numero, [.. permissoes.Select(c => PerfilMapping.Descrever(c, _catalogo).Nome)], link);

    private Task<IReadOnlyList<EnviarEmail>> EmitirAsync(
        ModeloDeAviso modelo, Guid fatoId, IReadOnlyList<DestinatarioDoAviso> destinatarios, IReadOnlyCollection<Identidade> excluidos, CancellationToken cancellationToken) =>
        _emissor.EmitirAsync(new PedidoDeAviso(modelo, fatoId, string.Empty, destinatarios, excluidos), cancellationToken);

    private async Task<List<DestinatarioDoAviso>> DecisoresAsync(IEnumerable<DecisaoDaVersao> descartadas, CancellationToken cancellationToken)
    {
        List<DestinatarioDoAviso> decisores = [];
        foreach (DecisaoDaVersao decisao in descartadas)
        {
            DestinatarioDoAviso? decisor = await IndividualAsync(
                decisao.Decisor, decisao.DecisorEmail, GruposDaGovernanca.DaEtapa(decisao.Etapa), cancellationToken).ConfigureAwait(false);
            decisores.AddRange(Lista(decisor));
        }

        return decisores;
    }

    // O e-mail gravado no ato vale mesmo que a pessoa tenha perdido a permissão depois; sem ele, vale o da concessão
    // ativa que ainda lhe dá a permissão pertinente; sem nenhum, o aviso não sai.
    private async Task<DestinatarioDoAviso?> IndividualAsync(
        Identidade pessoa, string? emailGravado, GrupoDaGovernanca grupo, CancellationToken cancellationToken)
    {
        if (emailGravado is not null)
        {
            return new DestinatarioDoAviso(pessoa, emailGravado);
        }

        List<DestinatarioDoAviso> titulares = await _grupos.ListarAsync(grupo, cancellationToken).ConfigureAwait(false);

        return titulares.Find(t => t.Pessoa == pessoa);
    }
}
