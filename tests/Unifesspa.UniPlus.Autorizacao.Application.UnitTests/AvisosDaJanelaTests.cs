namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;

/// <summary>
/// Os avisos da janela de migração: quem recebe a abertura, os lembretes e a retenção, quem fica de fora, o que o
/// corpo traz e o que acontece sem e-mail. Só personas fictícias do portal.
/// </summary>
public sealed class AvisosDaJanelaTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string EmailDiogo = "diogo.bruno.souza@uniplus-test.local";
    private const string EmailIsabella = "isabellapriscilafigueiredo@uniplus-test.local";
    private const string EmailMarlene = "marlenemarciamartins@uniplus-test.local";
    private const string EmailLeandro = "leandro-viana84@uniplus-test.local";
    private const string EmailBetina = "betina-dasneves81@uniplus-test.local";

    private static readonly CatalogoLivre Catalogo = new();
    private static readonly Identidade Leandro = new(Emissor, "leandro.viana");
    private static readonly Identidade Betina = new(Emissor, "betina.neves");
    private static readonly Identidade Isabella = new(Emissor, "isabella.figueiredo");
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly DateTimeOffset Instante = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "A abertura avisa quem aprova pela gestão e pela proteção de dados, sem repetir, e cada beneficiário pelo e-mail da concessão")]
    public async Task Abertura_AvisaAprovadoresEBeneficiarios()
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario();
        Concessao[] elegiveis =
        [
            ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Leandro, EmailLeandro).Construir(),
            ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Betina, EmailBetina).Construir(),
        ];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DaAberturaAsync(perfil, janela, elegiveis, ["a:b:c"], CancellationToken.None);

        enviados.Select(e => e.Destinatario).Should().BeEquivalentTo(
            [EmailDiogo, EmailIsabella, EmailMarlene, EmailLeandro, EmailBetina]);
    }

    [Theory(DisplayName = "Os lembretes de 48 e de 24 horas mandam os gestores agir e advertem só os beneficiários com pendência")]
    [InlineData(48)]
    [InlineData(24)]
    public async Task Lembrete_AvisaAprovadoresEAdvertePendentes(int horas)
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario();
        Concessao[] pendentes = [ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Leandro, EmailLeandro).Construir()];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DoLembreteAsync(perfil, janela, new LembreteDaJanela(horas, pendentes, [], true), CancellationToken.None);

        enviados.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailDiogo, EmailIsabella, EmailMarlene, EmailLeandro]);
        enviados.Single(e => e.Destinatario == EmailLeandro).Finalidade.Should().Be("perfil.advertencia_de_fim_da_migracao");
        enviados.Where(e => e.Destinatario != EmailLeandro).Select(e => e.Finalidade).Distinct().Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem a etapa da proteção de dados, o lembrete não chega a quem só aprova por ela")]
    public async Task Lembrete_SemEtapaDaProtecao_NaoAvisaAProtecaoDeDados()
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario();
        Concessao[] pendentes = [ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Leandro, EmailLeandro).Construir()];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DoLembreteAsync(perfil, janela, new LembreteDaJanela(24, pendentes, [], false), CancellationToken.None);

        enviados.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailDiogo, EmailIsabella, EmailLeandro]);
    }

    [Fact(DisplayName = "O dono da concessão retida recebe a advertência do prazo e os outros gestores, a mensagem de agir")]
    public async Task Lembrete_DonoDaRetida_RecebeAAdvertencia()
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario();
        Concessao[] retidas = [ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Diogo, EmailDiogo).Construir()];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DoLembreteAsync(perfil, janela, new LembreteDaJanela(24, retidas, retidas, false), CancellationToken.None);

        EnviarEmail dono = enviados.Single(e => e.Destinatario == EmailDiogo);
        dono.Finalidade.Should().Be("perfil.advertencia_de_fim_da_migracao");
        dono.CorpoTexto.Should().Contain("Horas restantes: 24");
        enviados.Single(e => e.Destinatario == EmailIsabella).Finalidade.Should().Be("perfil.janela_de_migracao_faltam_24_horas");
    }

    [Fact(DisplayName = "A retenção avisa os gestores, menos o dono da concessão retida, e adverte o dono")]
    public async Task Retida_AvisaGestoresMenosODono()
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario();
        Concessao[] retidas = [ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Isabella, EmailIsabella).Construir()];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DasRetidasAsync(perfil, janela, retidas, Instante, CancellationToken.None);

        enviados.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailDiogo, EmailIsabella]);
        enviados.Single(e => e.Destinatario == EmailIsabella).Finalidade.Should().Be("perfil.advertencia_de_fim_da_migracao");
        enviados.Single(e => e.Destinatario == EmailDiogo).Finalidade.Should().Be("perfil.concessao_retida_na_migracao");
    }

    [Fact(DisplayName = "O beneficiário sem e-mail na concessão não recebe aviso e a janela segue com os demais")]
    public async Task SemEmail_NaoRecebeAviso()
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario();
        Concessao[] elegiveis =
        [
            ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Leandro).Construir(),
            ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Betina, EmailBetina).Construir(),
        ];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DaAberturaAsync(perfil, janela, elegiveis, [], CancellationToken.None);

        enviados.Select(e => e.Destinatario).Should().Contain(EmailBetina).And.NotContain(EmailLeandro);
        enviados.Should().HaveCount(4);
    }

    [Fact(DisplayName = "Sem nenhum destinatário o aviso só vai ao log e nada entra no outbox")]
    public async Task SemDestinatario_SoLog()
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario(titulares: []);
        Concessao[] elegiveis = [ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Leandro).Construir()];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DaAberturaAsync(perfil, janela, elegiveis, [], CancellationToken.None);

        enviados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O corpo traz o perfil, a versão e o link e não traz nome nem e-mail de terceiros")]
    public async Task Corpo_NaoTrazDadosDeTerceiros()
    {
        (AvisosDaJanela avisos, Perfil perfil, JanelaDeMigracao janela) = Cenario();
        Concessao[] elegiveis = [ConcessaoBuilder.DoPerfil(perfil, Catalogo).Para(Leandro, EmailLeandro).Construir()];

        IReadOnlyList<EnviarEmail> enviados = await avisos.DaAberturaAsync(perfil, janela, elegiveis, [], CancellationToken.None);

        EnviarEmail paraDiogo = enviados.Single(e => e.Destinatario == EmailDiogo);
        paraDiogo.CorpoTexto.Should().Contain(perfil.Nome).And.Contain($"https://uniplus.test/configuracao/perfis/{perfil.Id}");
        foreach (string texto in new[] { paraDiogo.CorpoTexto, paraDiogo.CorpoHtml, paraDiogo.Assunto })
        {
            texto.Should().NotContain(EmailLeandro).And.NotContain(EmailIsabella).And.NotContain("Leandro");
        }
    }

    private static (AvisosDaJanela Avisos, Perfil Perfil, JanelaDeMigracao Janela) Cenario(TitularDaPermissao[]? titulares = null)
    {
        Perfil perfil = PerfisDeTeste.Vigente($"Perfil {Guid.NewGuid():N}", "Perfil de teste.", ["a:b:c"], Catalogo);
        JanelaDeMigracao janela = JanelaDeMigracao.Abrir(perfil.Id, perfil.Versoes[^1], Instante);
        AvisosDaJanela avisos = new(
            new EmissorDeAvisos(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
            new GruposDaGovernanca(new TitularesPorPermissao(titulares)),
            new LinkDeTeste(),
            new CatalogoDaAplicacao([]));

        return (avisos, perfil, janela);
    }

    // A gestão e a proteção de dados têm titulares próprios: Diogo aprova pela gestão, Marlene pela proteção de dados e Isabella pelas duas.
    private sealed class TitularesPorPermissao(TitularDaPermissao[]? titulares) : ITitularesDaPermissao
    {
        private static readonly string Gestao = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarFuncional).Codigo;
        private static readonly string Protecao = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados).Codigo;

        public Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TitularDaPermissao>> ListarTitularesComEmailAsync(
            string codigoDaPermissao, CancellationToken cancellationToken = default)
        {
            TitularDaPermissao[] todos = titulares ?? Padrao(codigoDaPermissao);

            return Task.FromResult<IReadOnlyList<TitularDaPermissao>>(codigoDaPermissao == Gestao || codigoDaPermissao == Protecao ? todos : []);
        }

        private static TitularDaPermissao[] Padrao(string codigo) =>
            codigo == Gestao
                ? [new(Emissor, Diogo.Sub, EmailDiogo), new(Emissor, Isabella.Sub, EmailIsabella)]
                : [new(Emissor, "marlene.martins", EmailMarlene), new(Emissor, Isabella.Sub, EmailIsabella)];
    }
}
