namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Os avisos da janela de migração: a abertura, os lembretes de 48 e de 24 horas e as concessões retidas. Cada um sai
/// uma vez por janela, tipo e destinatário, no outbox da transação que gravou o fato, e não leva motivo, nome de pessoa
/// nem e-mail. Quem tem as permissões de aprovar é avisado sempre; cada beneficiário recebe pelo e-mail da própria
/// concessão, e a que não tem e-mail só vai ao log.
/// </summary>
public sealed class AvisosDaJanela
{
    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;
    private readonly CatalogoDaAplicacao _catalogo;

    public AvisosDaJanela(
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

    /// <summary>A abertura avisa quem aprova pelas duas etapas e cada beneficiário elegível.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> DaAberturaAsync(
        Perfil perfil, JanelaDeMigracao janela, IReadOnlyList<Concessao> elegiveis, IReadOnlyList<string> acrescimo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(elegiveis);
        ArgumentNullException.ThrowIfNull(acrescimo);

        ModeloDeAviso modelo = Modelo(TipoDeAviso.JanelaDeMigracaoAberta, perfil, janela, elegiveis.Count, acrescimo);

        return await EmitirAsync(modelo, janela, [.. await AprovadoresAsync(cancellationToken).ConfigureAwait(false), .. Beneficiarios(elegiveis)], [], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>O lembrete de 48 ou de 24 horas avisa quem aprova e cada beneficiário com pendência.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> DoLembreteAsync(
        Perfil perfil, JanelaDeMigracao janela, int horas, IReadOnlyList<Concessao> pendentes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(pendentes);

        TipoDeAviso tipo = horas <= 24 ? TipoDeAviso.JanelaDeMigracaoFaltam24Horas : TipoDeAviso.JanelaDeMigracaoFaltam48Horas;
        ModeloDeAviso modelo = Modelo(tipo, perfil, janela, pendentes.Count, []);

        return await EmitirAsync(modelo, janela, [.. await AprovadoresAsync(cancellationToken).ConfigureAwait(false), .. Beneficiarios(pendentes)], [], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>A concessão retida avisa os gestores, menos o dono dela, e o dono.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> DasRetidasAsync(
        Perfil perfil, JanelaDeMigracao janela, IReadOnlyList<Concessao> retidas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(retidas);

        ModeloDeAviso modelo = Modelo(TipoDeAviso.ConcessaoRetidaNaMigracao, perfil, janela, retidas.Count, []);
        List<DestinatarioDoAviso> gestores = await _grupos.ListarAsync(GrupoDaGovernanca.Gestao, cancellationToken).ConfigureAwait(false);
        HashSet<Identidade> donos = [.. retidas.Select(static c => c.Beneficiario)];

        IReadOnlyList<EnviarEmail> dosGestores = await EmitirAsync(modelo, janela, gestores, donos, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> dosDonos = await EmitirAsync(modelo, janela, Beneficiarios(retidas), [], cancellationToken).ConfigureAwait(false);

        return [.. dosGestores, .. dosDonos];
    }

    // Quem aprova pela gestão ou pela proteção de dados.
    private Task<List<DestinatarioDoAviso>> AprovadoresAsync(CancellationToken cancellationToken) =>
        _grupos.ListarAsync([GrupoDaGovernanca.Gestao, GrupoDaGovernanca.ProtecaoDeDados], cancellationToken);

    private static List<DestinatarioDoAviso> Beneficiarios(IEnumerable<Concessao> concessoes) =>
        [.. concessoes.Where(static c => c.BeneficiarioEmail is not null).Select(static c => new DestinatarioDoAviso(c.Beneficiario, c.BeneficiarioEmail!))];

    private ModeloDeAviso Modelo(TipoDeAviso tipo, Perfil perfil, JanelaDeMigracao janela, int quantidade, IReadOnlyList<string> acrescimo) =>
        new(
            tipo,
            perfil.Nome,
            janela.NumeroDaVersaoNova,
            [.. acrescimo.Select(c => PerfilMapping.Descrever(c, _catalogo).Nome)],
            _link.DoPerfil(perfil.Id),
            new ContagensDoAviso(Pendentes: quantidade),
            janela.Prazo);

    private Task<IReadOnlyList<EnviarEmail>> EmitirAsync(
        ModeloDeAviso modelo,
        JanelaDeMigracao janela,
        IReadOnlyList<DestinatarioDoAviso> destinatarios,
        IReadOnlyCollection<Identidade> excluidos,
        CancellationToken cancellationToken) =>
        _emissor.EmitirAsync(new PedidoDeAviso(modelo, janela.Id, string.Empty, destinatarios, excluidos), cancellationToken);
}
