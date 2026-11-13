namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using System.Globalization;

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
    private const string MarcoDaRetida = "retida";

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

        return await EmitirAsync(modelo, janela, [.. await AprovadoresAsync(cancellationToken).ConfigureAwait(false), .. Beneficiarios(elegiveis)], [], string.Empty, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// O lembrete de 48 ou de 24 horas avisa os gestores de acessos, e os Representantes do controlador se há essa etapa,
    /// de que precisam agir, e
    /// cada beneficiário com pendência da advertência de que perderá o acesso. O dono de concessão retida não recebe a
    /// mensagem de agir: quem a decide é outro gestor.
    /// </summary>
    public async Task<IReadOnlyList<EnviarEmail>> DoLembreteAsync(
        Perfil perfil, JanelaDeMigracao janela, LembreteDaJanela lembrete, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(lembrete);

        IReadOnlyList<EnviarEmail> dosAprovadores = await AosQueDevemAgirAsync(perfil, janela, lembrete, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> dosBeneficiarios = await EmitirAsync(
            Advertencia(perfil, janela, lembrete.Horas), janela, Beneficiarios(lembrete.Pendentes), [], lembrete.Horas.ToString(CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);

        return [.. dosAprovadores, .. dosBeneficiarios];
    }

    /// <summary>A concessão retida avisa os gestores, menos o dono dela, e o dono, de que perderá o acesso no prazo.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> DasRetidasAsync(
        Perfil perfil, JanelaDeMigracao janela, IReadOnlyList<Concessao> retidas, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(retidas);

        ModeloDeAviso modelo = Modelo(TipoDeAviso.ConcessaoRetidaNaMigracao, perfil, janela, retidas.Count, []);
        List<DestinatarioDoAviso> gestores = await _grupos.ListarAsync(GrupoDaGovernanca.Gestao, cancellationToken).ConfigureAwait(false);
        int horas = (int)Math.Ceiling((janela.Prazo - agora).TotalHours);

        IReadOnlyList<EnviarEmail> dosGestores = await EmitirAsync(modelo, janela, gestores, Donos(retidas), string.Empty, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> dosDonos = await EmitirAsync(
            Advertencia(perfil, janela, horas), janela, Beneficiarios(retidas), [], MarcoDaRetida, cancellationToken)
            .ConfigureAwait(false);

        return [.. dosGestores, .. dosDonos];
    }

    private async Task<IReadOnlyList<EnviarEmail>> AosQueDevemAgirAsync(
        Perfil perfil, JanelaDeMigracao janela, LembreteDaJanela lembrete, CancellationToken cancellationToken)
    {
        TipoDeAviso tipo = lembrete.Horas <= 24 ? TipoDeAviso.JanelaDeMigracaoFaltam24Horas : TipoDeAviso.JanelaDeMigracaoFaltam48Horas;
        GrupoDaGovernanca[] grupos = lembrete.ComEtapaDaProtecao ? [GrupoDaGovernanca.Gestao, GrupoDaGovernanca.RepresentanteDoControlador] : [GrupoDaGovernanca.Gestao];
        List<DestinatarioDoAviso> aprovadores = await _grupos.ListarAsync(grupos, cancellationToken).ConfigureAwait(false);

        return await EmitirAsync(Modelo(tipo, perfil, janela, lembrete.Pendentes.Count, []), janela, aprovadores, Donos(lembrete.Retidas), string.Empty, cancellationToken)
            .ConfigureAwait(false);
    }

    private static HashSet<Identidade> Donos(IEnumerable<Concessao> retidas) => [.. retidas.Select(static c => c.Beneficiario)];

    private ModeloDeAviso Advertencia(Perfil perfil, JanelaDeMigracao janela, int horas) =>
        new(
            TipoDeAviso.AdvertenciaDeFimDaMigracao,
            perfil.Nome,
            janela.NumeroDaVersaoNova,
            [],
            _link.DosPedidos(),
            Prazo: janela.Prazo,
            HorasRestantes: horas);

    // Os gestores de acessos e os Representantes do controlador.
    private Task<List<DestinatarioDoAviso>> AprovadoresAsync(CancellationToken cancellationToken) =>
        _grupos.ListarAsync([GrupoDaGovernanca.Gestao, GrupoDaGovernanca.RepresentanteDoControlador], cancellationToken);

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
        string marco,
        CancellationToken cancellationToken) =>
        _emissor.EmitirAsync(new PedidoDeAviso(modelo, janela.Id, marco, destinatarios, excluidos), cancellationToken);
}

/// <summary>O que o lembrete de 48 ou de 24 horas precisa saber da janela: as horas, as concessões pendentes e retidas e se há a etapa da proteção de dados.</summary>
public sealed record LembreteDaJanela(int Horas, IReadOnlyList<Concessao> Pendentes, IReadOnlyList<Concessao> Retidas, bool ComEtapaDaProtecao);
