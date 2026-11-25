namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.Registro;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services.ListagemDeInscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class RegistroDaAplicacaoTests
{
    public static TheoryData<Type> Servicos => new()
    {
        typeof(InscricaoDoDono), typeof(PeriodoDaInscricaoResolver), typeof(VersaoDaInscricaoResolver),
        typeof(ContextoDaVersaoFixadaResolver), typeof(InicioDaInscricao), typeof(DescarteDaInscricao),
        typeof(CancelamentoDaInscricaoPeloTitular), typeof(ConferenciaDoEnderecoDaInscricao),
        typeof(ConferenciaDosMunicipiosDaInscricao), typeof(PreparacaoDoEnvioDaInscricao),
        typeof(ConferenciaDoEnvioDaInscricao), typeof(RegistroDoEnvioDaInscricao), typeof(SubmissaoDaInscricao),
        typeof(LeituraDaInscricao), typeof(ConsultaDaAvaliacaoDaInscricao), typeof(PreparacaoDaAvaliacaoDaInscricao),
        typeof(ProcessosDasInscricoes), typeof(DocumentosEnviadosDaInscricao), typeof(AcessoAoDocumentoDaInscricao), typeof(EmissaoDoAcessoAoArquivo),
        typeof(ClassificacaoDaExigenciaDoArquivo), typeof(InicioDoEnvioDeDocumento), typeof(ConfirmacaoDoEnvioDeDocumento), typeof(RemocaoDoArquivoDaInscricao),
        typeof(PrazoDaCorrecaoDoComprovante), typeof(CancelamentoDoComprovante), typeof(MontadorDoComprovante),
        typeof(ProjetorDaInscricaoAdministrativa), typeof(LeituraDaInscricaoAdministrativa),
        typeof(ProjetorDaListagemDeInscricoes), typeof(ResolvedorDoEscopoDeNotasDoEnem),
        typeof(PreparacaoDaImportacaoDeNotas), typeof(ImportacaoDasNotasDoEnem),
        typeof(DetalheDoLoteDeNotasMontador), typeof(GeracaoDoEnvioAoInep),
    };

    [Theory]
    [MemberData(nameof(Servicos))]
    public void AddSelecaoApplication_ServicoSolicitadoNoEscopo_ResolveServico(Type tipo)
    {
        ServiceCollection servicos = CriarServicos();
        using ServiceProvider provedor = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope escopo = provedor.CreateScope();
        escopo.ServiceProvider.GetRequiredService(tipo).Should().BeOfType(tipo);
    }

    [Theory]
    [MemberData(nameof(Servicos))]
    public void AddSelecaoApplication_ServicoSolicitado_RegistraUmaVezComTempoDeVidaDeEscopo(Type tipo)
    {
        ServiceCollection servicos = CriarServicos();
        ServiceDescriptor registro = servicos.Should().ContainSingle(item => item.ServiceType == tipo).Subject;
        registro.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    private static ServiceCollection CriarServicos()
    {
        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddOptions();
        servicos.AddSelecaoApplication();
        PortasDaInscricaoDeTeste.AdicionarA(servicos);
        return servicos;
    }
}
