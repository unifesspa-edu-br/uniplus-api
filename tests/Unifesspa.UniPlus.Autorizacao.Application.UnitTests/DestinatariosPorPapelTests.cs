namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using System.Diagnostics.Metrics;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>
/// Quem a governança de perfis avisa, por papel: os gestores de acessos que mantêm perfis e os Representantes do
/// controlador, menos quem praticou o ato; o Encarregado de dados, que só consulta as versões, não recebe aviso de
/// decisão. Isabella e Diogo são gestores, Marlene é Representante, Leandro é o Encarregado e Betina pratica o ato;
/// Diogo também criou a versão pendente. A vigência da concessão é conferida no banco, pelos testes do Host.
/// </summary>
public sealed class DestinatariosPorPapelTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string EmailIsabella = "isabellapriscilafigueiredo@uniplus-test.local";
    private const string EmailMarlene = "marlenemarciamartins@uniplus-test.local";
    private const string EmailLeandro = "leandro-viana84@uniplus-test.local";
    private const string Interna = "selecao:processos:consultar";
    private const string Removida = "selecao:processos:operar";

    [Fact(DisplayName = "A etapa protegida da versão é decidida pelo grupo do Representante do controlador")]
    public void EtapaProtegida_EDoRepresentante()
    {
        GruposDaGovernanca.DaEtapa(EtapaDaVersao.ProtecaoDeDados).Should().Be(GrupoDaGovernanca.RepresentanteDoControlador);
        GruposDaGovernanca.DaEtapa(EtapaDaVersao.GestaoDeAcessos).Should().Be(GrupoDaGovernanca.Gestao);
    }

    [Fact(DisplayName = "O grupo do Representante é resolvido pela permissão reservada de aprovar pela proteção de dados")]
    public async Task GrupoDoRepresentante_ResolvidoPelaPermissaoReservada()
    {
        List<DestinatarioDoAviso> representantes = await new GruposDaGovernanca(new TitularesPorPapel())
            .ListarAsync(GrupoDaGovernanca.RepresentanteDoControlador, CancellationToken.None);

        representantes.Select(static d => d.Email).Should().Equal(EmailMarlene);
    }

    [Fact(DisplayName = "A suspensão avisa gestores e Representantes, e o Encarregado de dados não")]
    public async Task Suspensao_AvisaGestoresERepresentantesMenosOEncarregado()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", [Interna], new CatalogoDeTeste());
        perfil.Suspender(PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos, "Suspenso para o teste.", PerfisDeTeste.Instante, new CatalogoDeTeste())
            .IsSuccess.Should().BeTrue();

        IReadOnlyList<EnviarEmail> avisos = await Contencao().DaSuspensaoAsync(perfil, perfil.Suspensoes[^1], CancellationToken.None);

        Destinatarios(avisos).Should().Equal(Ordenados(TitularesPorPapel.EmailDoAutor, EmailIsabella, EmailMarlene));
    }

    [Fact(DisplayName = "A remoção de permissão interna também avisa o Representante, e não quem removeu nem o Encarregado")]
    public async Task RemocaoInterna_AvisaORepresentante()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", [Interna, Removida], new CatalogoDeTeste());
        TrocaDeVigencia troca = perfil.Remover(Remocao(), new CatalogoDeTeste()).Troca!;

        IReadOnlyList<EnviarEmail> avisos = await Versao().DaRemocaoAsync(perfil, troca, PerfisDeTeste.Gestor, CancellationToken.None);

        Destinatarios(avisos).Should().Equal(Ordenados(TitularesPorPapel.EmailDoAutor, EmailIsabella, EmailMarlene));
    }

    [Fact(DisplayName = "O cancelamento por contenção avisa quem criou, os demais gestores e os Representantes, e não quem removeu")]
    public async Task CancelamentoPorContencao_AvisaCriadorGestoresERepresentantes()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Análise de atendimento", "Analisa os pedidos.", [Interna], new CatalogoDeTeste());
        VersaoDoPerfil cancelada = perfil.VersaoPendente!;

        IReadOnlyList<EnviarEmail> avisos = await Versao().DoCancelamentoPorContencaoAsync(perfil, cancelada, PerfisDeTeste.Gestor, CancellationToken.None);

        Destinatarios(avisos).Should().Equal(Ordenados(cancelada.CriadaPorEmail!, EmailIsabella, EmailMarlene), "o criador é avisado uma vez, pelo e-mail gravado na versão");
    }

    [Fact(DisplayName = "Sem Representante vigente, a versão apta à etapa protegida não avisa ninguém e a ausência soma no contador")]
    public async Task SemRepresentanteVigente_NinguemEAvisadoEAAusenciaConta()
    {
        using MetricasDaGovernancaDePerfis metricas = new(TimeProvider.System);
        using ContadorDeAusencias ausencias = new(metricas);
        Perfil perfil = AptaAEtapaProtegida();
        EmissorDeAvisos emissor = new(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), metricas, NullLogger<EmissorDeAvisos>.Instance);
        AvisoDeEtapaApta aviso = new(
            emissor, new GruposDaGovernanca(new TitularesPorPapel(semRepresentante: true)), new LinkDeTeste(), new CatalogoDaAplicacao([]), new CenarioDaGovernancaDaVersao.RelogioFixo(PerfisDeTeste.Instante.AddHours(1)));

        IReadOnlyList<EnviarEmail> avisos = await aviso.EmitirAsync(perfil, CancellationToken.None);

        avisos.Should().BeEmpty();
        ausencias.Por(TipoDeAviso.VersaoPendenteApta.ToCodigo()).Should().Be(1);
    }

    // Versão pendente com dado pessoal, documento verificado e aprovada pela gestão: falta só a etapa protegida.
    private static Perfil AptaAEtapaProtegida()
    {
        CatalogoMutavelDeTeste catalogo = new();
        Perfil perfil = PerfisDeTeste.Pendente("Acompanhamento", "Acompanha os processos.", [CatalogoMutavelDeTeste.Dados], catalogo);
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(perfil.ConfirmarDocumento(versao.Id, "etag", PerfisDeTeste.Instante));
        perfil.RegistrarDocumentoVerificado(documento.Id, new string('a', 64), PerfisDeTeste.Instante).Should().BeTrue();
        PerfisDeTeste.Decidir(perfil, versao, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.Autoria(PerfisDeTeste.Gestor), catalogo).Concluida.Should().BeTrue();

        return perfil;
    }

    private static PedidoDeRemocao Remocao() =>
        new([Removida], "Permissão concedida por engano.", null, PerfisDeTeste.AutoriaDoGestor, PerfisDeTeste.Instante);

    private static string[] Destinatarios(IEnumerable<EnviarEmail> avisos) => [.. avisos.Select(static a => a.Destinatario).Order(StringComparer.Ordinal)];

    private static string[] Ordenados(params string[] emails) => [.. emails.Order(StringComparer.Ordinal)];

    private static EmissorDeAvisos NovoEmissor() =>
        new(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance);

    private static AvisosDaContencao Contencao() => new(NovoEmissor(), new GruposDaGovernanca(new TitularesPorPapel()), new LinkDeTeste());

    private static AvisosDaVersao Versao() =>
        new(NovoEmissor(), new GruposDaGovernanca(new TitularesPorPapel()), new LinkDeTeste(), new CatalogoDaAplicacao([]));

    /// <summary>Os titulares por permissão, como a composição dos perfis de governança os dá.</summary>
    private sealed class TitularesPorPapel(bool semRepresentante = false) : ITitularesDaPermissao
    {
        private static readonly string AprovarPelaProtecao = Codigo(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados);

        public const string EmailDoAutor = "diogo.bruno.souza@uniplus-test.local";

        private static readonly Dictionary<string, TitularDaPermissao[]> PorPermissao = new(StringComparer.Ordinal)
        {
            [Codigo(PermissaoAutorizacao.PerfisAprovarFuncional)] = [new(Emissor, "isabella.figueiredo", EmailIsabella)],
            [Codigo(PermissaoAutorizacao.PerfisManter)] =
            [
                new(Emissor, "isabella.figueiredo", EmailIsabella),
                new(Emissor, PerfisDeTeste.Gestor.Sub, "betina-dasneves81@uniplus-test.local"),
                new(Emissor, PerfisDeTeste.Autor.Sub, EmailDoAutor),
            ],
            [Codigo(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados)] = [new(Emissor, "marlene.martins", EmailMarlene)],
            [Codigo(PermissaoAutorizacao.PerfisConsultarVersoes)] = [new(Emissor, "leandro.viana", EmailLeandro)],
        };

        public Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TitularDaPermissao>> ListarTitularesComEmailAsync(
            string codigoDaPermissao, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TitularDaPermissao>>(
                semRepresentante && codigoDaPermissao == AprovarPelaProtecao ? [] : PorPermissao.GetValueOrDefault(codigoDaPermissao, []));

        private static string Codigo(PermissaoAutorizacao permissao) => CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Codigo;
    }

    /// <summary>Escuta o contador real de avisos sem destinatário do medidor da governança, por tipo.</summary>
    private sealed class ContadorDeAusencias : IDisposable
    {
        private readonly MeterListener _ouvinte = new();
        private readonly Dictionary<string, long> _porTipo = new(StringComparer.Ordinal);

        public ContadorDeAusencias(MetricasDaGovernancaDePerfis metricas)
        {
            _ouvinte.InstrumentPublished = (instrumento, ouvinte) =>
            {
                if (instrumento.Meter == metricas.Medidor && instrumento.Name == "uniplus.autorizacao.avisos_sem_destinatario")
                {
                    ouvinte.EnableMeasurementEvents(instrumento);
                }
            };
            _ouvinte.SetMeasurementEventCallback<long>((_, valor, tags, _) => Somar(tags, valor));
            _ouvinte.Start();
        }

        public long Por(string tipo) => _porTipo.GetValueOrDefault(tipo);

        public void Dispose() => _ouvinte.Dispose();

        private void Somar(ReadOnlySpan<KeyValuePair<string, object?>> tags, long valor)
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "tipo" && tag.Value is string tipo)
                {
                    _porTipo[tipo] = Por(tipo) + valor;
                }
            }
        }
    }
}
