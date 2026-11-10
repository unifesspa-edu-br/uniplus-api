namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.TitularesDaGovernanca;

/// <summary>
/// O aviso de que a versão pendente ficou apta na etapa: só com o documento verificado, no prazo e pendente, a quem
/// aprova a etapa em que ela está, sem repetir. Só personas fictícias do portal.
/// </summary>
public sealed class AvisoDeEtapaAptaTests
{
    private const string Dados = CatalogoMutavelDeTeste.Dados;
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly DateTimeOffset Depois = PerfisDeTeste.Instante.AddHours(1);

    [Fact(DisplayName = "Com o documento ainda não verificado, a versão pendente não avisa ninguém")]
    public async Task DocumentoNaoVerificado_NaoAvisa()
    {
        (Perfil perfil, _) = Pendente(verificar: false);

        (await Emissor(Depois).EmitirAsync(perfil, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Verificado o documento, a etapa da gestão avisa os gestores, menos quem criou a versão")]
    public async Task DocumentoVerificado_AvisaAGestaoMenosOCriador()
    {
        (Perfil perfil, _) = Pendente(verificar: true);

        IReadOnlyList<EnviarEmail> enviados = await Emissor(Depois).EmitirAsync(perfil, CancellationToken.None);

        enviados.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailIsabella]);
        enviados.Should().OnlyContain(e => e.Finalidade == TipoDeAviso.VersaoPendenteApta.Finalidade());
    }

    [Fact(DisplayName = "A versão vencida não avisa, porque já não aprova")]
    public async Task VersaoVencida_NaoAvisa()
    {
        (Perfil perfil, VersaoDoPerfil versao) = Pendente(verificar: true);

        (await Emissor(versao.ExpiraEm!.Value).EmitirAsync(perfil, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A versão em vigor, que não está pendente, não avisa")]
    public async Task VersaoVigente_NaoAvisa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["a:b:c"], new CatalogoLivre());

        (await Emissor(Depois).EmitirAsync(perfil, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Repetir a chamada para a mesma versão, ciclo e etapa não avisa de novo")]
    public async Task MesmaEtapa_NaoRepete()
    {
        (Perfil perfil, _) = Pendente(verificar: true);
        AvisoDeEtapaApta emissor = Emissor(Depois);
        await emissor.EmitirAsync(perfil, CancellationToken.None);

        (await emissor.EmitirAsync(perfil, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Aprovada pela gestão, a etapa da proteção de dados avisa quem a aprova, menos quem aprovou pela gestão")]
    public async Task AprovadaPelaGestao_AvisaAProtecaoDeDadosMenosQuemAprovou()
    {
        (Perfil perfil, VersaoDoPerfil versao) = Pendente(verificar: true);
        AvisoDeEtapaApta emissor = Emissor(Depois);
        await emissor.EmitirAsync(perfil, CancellationToken.None);
        PerfisDeTeste.Decidir(perfil, versao, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.Autoria(Isabella), new CatalogoMutavelDeTeste()).Concluida.Should().BeTrue();

        IReadOnlyList<EnviarEmail> enviados = await emissor.EmitirAsync(perfil, CancellationToken.None);

        enviados.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailMarlene]);
    }

    [Fact(DisplayName = "Depois da subida de ciclo, a etapa da gestão avisa de novo, porque o ciclo é outro")]
    public async Task SubidaDeCiclo_AvisaAGestaoDeNovo()
    {
        CatalogoMutavelDeTeste catalogo = new();
        (Perfil perfil, VersaoDoPerfil versao) = Pendente(verificar: true);
        AvisoDeEtapaApta emissor = Emissor(Depois);
        await emissor.EmitirAsync(perfil, CancellationToken.None);
        PerfisDeTeste.Decidir(perfil, versao, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.Autoria(Isabella), catalogo);
        catalogo.SensibilidadeDosDados = ClassificacaoDeProtecao.Interno;
        PerfisDeTeste.Decidir(perfil, versao, EtapaDaVersao.ProtecaoDeDados, PerfisDeTeste.Autoria(Marlene), catalogo).Recalculo.Should().NotBeNull();

        IReadOnlyList<EnviarEmail> enviados = await emissor.EmitirAsync(perfil, CancellationToken.None);

        versao.CicloDeAprovacao.Should().Be(2);
        enviados.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailIsabella]);
    }

    private static (Perfil Perfil, VersaoDoPerfil Versao) Pendente(bool verificar)
    {
        CatalogoMutavelDeTeste catalogo = new();
        Perfil perfil = PerfisDeTeste.Pendente("Acompanhamento", "Acompanha os processos.", [Dados], catalogo);
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(perfil.ConfirmarDocumento(versao.Id, "etag", PerfisDeTeste.Instante));
        if (verificar)
        {
            perfil.RegistrarDocumentoVerificado(documento.Id, Sha, PerfisDeTeste.Instante).Should().BeTrue();
        }

        return (perfil, versao);
    }

    private static AvisoDeEtapaApta Emissor(DateTimeOffset agora) =>
        new(
            new EmissorDeAvisos(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
            new GruposDaGovernanca(new TitularesDaGovernanca()),
            new LinkDeTeste(),
            new CatalogoDaAplicacao([]),
            new CenarioDaGovernancaDaVersao.RelogioFixo(agora));
}
