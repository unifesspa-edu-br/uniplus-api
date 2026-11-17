namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A restauração confere só a integridade e a referência do envelope e acumula todas as causas (ADR-0125). A completude não
/// entra: o envelope já a atendeu quando foi publicado, e reconferi-la com a regra de hoje reinterpretaria o passado.
/// </summary>
public sealed class RecusasAcumuladasNaRestauracaoTests
{
    private const int EtapaSemId = 1;
    private const int EtapaComIdRepetido = 2;
    private const int OrdemDeEtapaRepetida = 3;
    private const int DistribuicaoVazia = 5;
    private const int OrdemDeDesempateRepetida = 8;
    private const int CronogramaVazio = 11;
    private const int EtapaComFaseInexistente = 14;
    private const int FaseAgrupadoraSemEtapa = 15;
    private const int EtapaSemFaseDeclarada = 16;

    private static Result Restaurar(int[] problemas) => Restaurar(OrdemDasRecusasDoGrafoTests.GrafoComProblemas(problemas));

    private static Result Restaurar(GrafoConfiguracao grafo)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        return processo.RestaurarConfiguracaoCongelada(versao, grafo);
    }

    [Theory(DisplayName = "O envelope com etapa sem fase declarada ou com fase agrupadora sem etapa restaura: é completude, recusada só ao congelar")]
    [InlineData(EtapaSemFaseDeclarada)]
    [InlineData(FaseAgrupadoraSemEtapa)]
    public void Restaurar_CompletudeIncompleta_Restaura(int problema)
    {
        Result resultado = Restaurar([problema]);

        resultado.IsSuccess.Should().BeTrue(string.Join(", ", resultado.Errors.Select(static causa => causa.Error.Code)));
    }

    [Fact(DisplayName = "O envelope com etapa que declara fase inexistente no cronograma é recusado, citando o campo da etapa")]
    public void Restaurar_EtapaComFaseInexistente_Recusa()
    {
        Result resultado = Restaurar([EtapaComFaseInexistente]);

        FieldError causa = resultado.Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseNoCronograma");
        causa.Field.Should().Be("etapas[0].faseChave");
    }

    [Fact(DisplayName = "O grafo com problemas em várias famílias devolve todas as causas, na ordem em que as famílias são conferidas")]
    public void Restaurar_VariasFamilias_AcumulaNaOrdemDasFamilias()
    {
        Result resultado = Restaurar([EtapaComIdRepetido, DistribuicaoVazia, OrdemDeDesempateRepetida, CronogramaVazio]);

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("etapas[1].chave", "ProcessoSeletivo.IdEtapaDuplicado"),
            ("etapas", "ProcessoSeletivo.NenhumaEtapaComponeNota"),
            ("distribuicaoVagas", "ProcessoSeletivo.DistribuicaoVagasVazia"),
            ("classificacao", "ProcessoSeletivo.OrdemDesempateDuplicada"),
            ("cronograma.fases", "ProcessoSeletivo.CronogramaFasesVazio"));
    }

    [Fact(DisplayName = "As causas da mesma família saem juntas, na posição de entrada")]
    public void Restaurar_MesmaFamilia_AcumulaNaPosicaoDeEntrada()
    {
        Result resultado = Restaurar([EtapaComIdRepetido, OrdemDeEtapaRepetida]);

        resultado.Errors.Select(static causa => causa.Error.Code).Should().Contain("ProcessoSeletivo.IdEtapaDuplicado")
            .And.Contain("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "A etapa sem Id impede avaliar as demais regras das etapas e a fase inexistente, que não têm o que julgar")]
    public void Restaurar_EtapaSemId_ImpedeAvaliarAsRegrasDasEtapas()
    {
        Result resultado = Restaurar([EtapaSemId, OrdemDeEtapaRepetida, EtapaComFaseInexistente, DistribuicaoVazia]);


        resultado.Errors.Select(static causa => causa.Error.Code).Should().Equal(
            "ProcessoSeletivo.IdEtapaAusente", "ProcessoSeletivo.DistribuicaoVagasVazia");
    }

    [Fact(DisplayName = "A versão de outro processo impede conferir o grafo")]
    public void Restaurar_VersaoDeOutroProcesso_ImpedeConferirOGrafo()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out _);
        _ = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao deOutro);

        Result resultado = processo.RestaurarConfiguracaoCongelada(deOutro, OrdemDasRecusasDoGrafoTests.GrafoComProblemas([DistribuicaoVazia]));

        resultado.Errors.Select(static causa => causa.Error.Code).Should().Equal("VersaoConfiguracao.VersaoDeOutroProcesso");
    }

    [Fact(DisplayName = "A restauração recusada não altera o processo")]
    public void Restaurar_Recusada_NaoAlteraOProcesso()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);
        int ofertasAntes = processo.DistribuicaoVagas.Count;

        processo.RestaurarConfiguracaoCongelada(versao, OrdemDasRecusasDoGrafoTests.GrafoComProblemas([DistribuicaoVazia])).IsFailure.Should().BeTrue();

        processo.DistribuicaoVagas.Should().HaveCount(ofertasAntes);
    }
}
