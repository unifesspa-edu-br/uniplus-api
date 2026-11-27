namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.VagasDoDocumentoDeTeste;

public sealed class VagasLidasTests
{
    private static VagasLidas Ler(VagasNoDocumento? vagas) => VagasLidas.Ler(Declaracoes(vagas), Cadastros());

    private static VagasNoDocumento Completas() => Vagas(Distribuicao(0), Distribuicao(1, OfertaDeMedicina)) with
    {
        Cascata = Cascata(DestinosConformes()),
        Fada = FadaConforme(),
        Atendimento = AtendimentoConforme(),
    };

    [Fact(DisplayName = "As vagas conformes viram a declaração com as opções de curso, a distribuição, a cascata, o FADA e o atendimento")]
    public void VagasConformes_ViramADeclaracao()
    {
        VagasLidas lidas = Ler(Completas());

        lidas.Origem.PartesRecusadas.Should().BeEmpty();
        lidas.Vagas.NumeroDeOpcoesDeCurso.Should().Be(1);
        lidas.Vagas.DistribuicaoVagas.Should().HaveCount(2);
        lidas.Vagas.AplicaFada.Should().BeTrue();
        lidas.Vagas.Fada.Should().NotBeNull();
        lidas.Vagas.Cascata.Should().NotBeNull();
        lidas.Vagas.AtendimentoEspecializado.Should().NotBeNull();
    }

    [Fact(DisplayName = "O documento sem o bloco das vagas é a declaração vazia, sem recusa")]
    public void BlocoAusente_EhADeclaracaoVazia()
    {
        VagasLidas lidas = Ler(null);

        lidas.Vagas.DistribuicaoVagas.Should().BeEmpty();
        lidas.Vagas.NumeroDeOpcoesDeCurso.Should().BeNull();
        lidas.Vagas.AplicaFada.Should().BeNull();
        lidas.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "As listas vazias e os blocos ausentes não têm o que ler nem recusar")]
    public void ListasVaziasEBlocosAusentes_NaoTemRecusa()
    {
        VagasLidas lidas = Ler(new(null, []));

        lidas.Vagas.DistribuicaoVagas.Should().BeEmpty();
        lidas.Vagas.Cascata.Should().BeNull();
        lidas.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O número de opções de curso fora de 1 e 2 é recusado no campo do número, e as outras partes seguem lidas")]
    public void OpcoesDeCursoInvalidas_RecusaNoCampoDoNumero()
    {
        VagasLidas lidas = Ler(Completas() with { OpcoesDeCurso = 3 });

        Causas(lidas.Origem.PartesRecusadas).Should().Equal(("/opcoesDeCurso/numero", "ProcessoSeletivo.OpcoesDeCursoInvalido"));
        lidas.Vagas.NumeroDeOpcoesDeCurso.Should().BeNull();
        lidas.Vagas.DistribuicaoVagas.Should().HaveCount(2);
    }

    private static VagasNoDocumento ComTodasAsPartesRecusadas() => new(0, [Distribuicao(0, TextoDigitado), Distribuicao(1)])
    {
        Cascata = Cascata(DestinosConformes()) with { Regra = new(TextoDigitado, "v1") },
        Atendimento = new([new(0, TextoDigitado)], [], []),
        Fada = FadaConforme(aplica: false),
    };

    [Fact(DisplayName = "As recusas de todas as partes voltam juntas, por bloco, na ordem do documento, e a parte recusada não impede as demais")]
    public void RecusasDeVariasPartes_VoltamJuntasPorBloco()
    {
        VagasLidas lidas = Ler(ComTodasAsPartesRecusadas());

        Causas(lidas.Origem.PartesRecusadas).Should().Equal(
            ("/opcoesDeCurso/numero", "ProcessoSeletivo.OpcoesDeCursoInvalido"),
            ("/distribuicaoVagas/0/ofertaCursoCodigo", "ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada"),
            ("/ordemDeRemanejamento/regra/codigo", "ConfiguracaoCascataRemanejamento.RegraNaoEncontrada"),
            ("/atendimento/condicoes/0/condicaoCodigo", "AtendimentoEspecializado.CondicaoNaoEncontrada"),
            ("/fada", "ProcessoSeletivo.FadaNaoAplicaComConfiguracao"));
        lidas.Vagas.DistribuicaoVagas.Should().ContainSingle();
        lidas.Vagas.Cascata.Should().BeNull();
        lidas.Vagas.AtendimentoEspecializado.Should().BeNull();
        lidas.Vagas.AplicaFada.Should().BeNull();
    }

    [Fact(DisplayName = "A origem junta a posição enviada de cada lista e os caminhos das partes recusadas")]
    public void Origem_JuntaAsListasEAsPartesRecusadas()
    {
        VagasNoDocumento vagas = Vagas(Distribuicao(2, TextoDigitado), Distribuicao(6)) with
        {
            Cascata = Cascata(Destino(4, CotaA, CotaB), Destino(9, CotaA, Ampla), Destino(11, CotaB, Ampla)),
        };

        Domain.ProcessosSeletivos.FasesEEtapas.OrigemNoDocumento origem = Ler(vagas).Origem;

        origem.PosicaoDeOrigem(LeituraDasDistribuicoesDeVagas.Lista, 0).Should().Be(6);
        origem.PosicaoDeOrigem(LeituraDaCascataDeRemanejamento.Lista, 1).Should().Be(9);
        origem.TemParteRecusadaEm(LeituraDasDistribuicoesDeVagas.Lista).Should().BeTrue();
        origem.TemParteRecusadaEm(LeituraDaCascataDeRemanejamento.Caminho).Should().BeFalse();
    }

    [Fact(DisplayName = "A leitura recusa o documento sem lançar exceção quando os campos vêm vazios ou nulos")]
    public void CamposNulos_NaoLancam()
    {
        DistribuicaoNoDocumento vazia = new(0, null, null, []);
        VagasNoDocumento vagas = new(null, [vazia])
        {
            Cascata = new(null, null, [new(0, null, null)]),
            Fada = new(true, new(null, null), null, null, null, null),
            Atendimento = new([new(0, null)], [new(0, null)], [new(0, null)]),
        };

        VagasLidas lidas = Ler(vagas);

        lidas.Origem.PartesRecusadas.Should().NotBeEmpty();
        lidas.Vagas.DistribuicaoVagas.Should().BeEmpty();
    }

    [Fact(DisplayName = "As distribuições adotadas voltam com a posição que tinham na lista enviada, e a recusada não aparece")]
    public void DistribuicoesNaPosicaoDoDocumento_TrazAPosicaoEnviadaDeCadaAdotada()
    {
        VagasLidas lidas = Ler(Vagas(Distribuicao(2, TextoDigitado), Distribuicao(6), Distribuicao(9, OfertaDeMedicina)));

        lidas.DistribuicoesNaPosicaoDoDocumento().Select(static adotada => adotada.PosicaoNoDocumento).Should().Equal(6, 9);
        lidas.DistribuicoesNaPosicaoDoDocumento().Select(static adotada => adotada.Distribuicao).Should().Equal(lidas.Vagas.DistribuicaoVagas);
    }

    [Fact(DisplayName = "O documento sem vagas não tem distribuição em posição alguma")]
    public void DistribuicoesNaPosicaoDoDocumento_BlocoAusente_EhVazio()
    {
        Ler(null).DistribuicoesNaPosicaoDoDocumento().Should().BeEmpty();
    }
}
