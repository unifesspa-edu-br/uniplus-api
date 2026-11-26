namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

public sealed class ConsequenciasDasBancasTests
{
    [Theory]
    [InlineData(ConsequenciaDoDescumprimento.RetiraOAcrescimo, true)]
    [InlineData(ConsequenciaDoDescumprimento.EliminaDoProcesso, false)]
    public void Acrescimo_BancaDaEtapa_ConfereSeOFatorEOferecido(ConsequenciaDoDescumprimento consequencia, bool recusa)
    {
        ProcessoSeletivo processo = ProcessoComBanca(consequencia);
        ConsequenciasDasBancas.Acrescimo(processo.Declaracao(), CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)).Any().Should().Be(recusa);
    }

    [Fact]
    public void Acrescimo_BancaComFatorOferecido_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        ConfiguracaoDoFada fada = ConfiguracaoDoFada.Criar(
            ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", HashFixo), 1.2m, null,
            FormularioDeTeste.FatoBooleanoDaInscricao, new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA", "PORTARIA", "Portaria", "Inclusão regional", [("1504208", "Marabá", "PA")])).Value!;
        processo.DefinirFada(true, fada).IsSuccess.Should().BeTrue();
        ConsequenciasDasBancas.Acrescimo(processo.Declaracao(), CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(ConsequenciaDoDescumprimento.RetiraAModalidade, true)]
    [InlineData(ConsequenciaDoDescumprimento.EliminaDoProcesso, false)]
    public void SemModalidade_BancaSemExigencias_ConfereConsequencia(ConsequenciaDoDescumprimento consequencia, bool recusa)
    {
        ProcessoSeletivo processo = ProcessoComBanca(consequencia);
        ConsequenciasDasBancas.SemModalidade(processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade, CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)).Any().Should().Be(recusa);
    }

    [Theory]
    [InlineData("LB_PPI", false)]
    [InlineData("INEXISTENTE", true)]
    public void SemModalidade_ExigenciaDaEtapa_ConfereModalidadeAlcancada(string codigo, bool recusa)
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.RetiraAModalidade);
        DocumentoExigido exigencia = ExigenciaDaEtapa(processo, codigo);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();
        ConsequenciasDasBancas.SemModalidade(processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade, CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)).Any().Should().Be(recusa);
    }

    [Theory]
    [InlineData("LB_PPI")]
    [InlineData("INEXISTENTE")]
    public void SemAmpla_OfertaComAmpla_NaoRecusa(string codigo)
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.RetiraAModalidade);
        DocumentoExigido exigencia = ExigenciaDaEtapa(processo, codigo);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();
        ConsequenciasDasBancas.SemAmpla(processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade,
            CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("LB_PPI", true)]
    [InlineData("INEXISTENTE", false)]
    public void SemAmpla_OfertaSemAmpla_RecusaQuandoExigenciaAlcancaOferta(string codigo, bool recusa)
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.RetiraAModalidade);
        ConsequenciasDaRetiradaDaModalidadeTests.RetirarAmplaDaOferta(processo);
        DocumentoExigido exigencia = ExigenciaDaEtapa(processo, codigo);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();
        ConsequenciasDasBancas.SemAmpla(processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade,
            CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)).Any().Should().Be(recusa);
    }

    [Fact]
    public void Violacoes_BancaQueAvalia_AceitaConsequencia()
    {
        TipoDeBancaCopiado tipo = TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista);
        ConsequenciaDaAusenciaNaBanca.Violacoes([(tipo, ConsequenciaDoDescumprimento.EliminaDoProcesso)], "bancas").Should().BeEmpty();
    }

    [Fact]
    public void Violacoes_BancasQueNaoAvaliam_AcumulamNaPosicao()
    {
        TipoDeBancaCopiado tipo = TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDeRecursos);
        FieldError[] recusas = [.. ConsequenciaDaAusenciaNaBanca.Violacoes(
            [(tipo, ConsequenciaDoDescumprimento.EliminaDoProcesso), (tipo, null), (tipo, ConsequenciaDoDescumprimento.RetiraAModalidade)], "bancas")];
        recusas.Select(static recusa => recusa.Field).Should().Equal("bancas[0].consequenciaDaAusencia", "bancas[2].consequenciaDaAusencia");
    }

    [Theory]
    [InlineData("banca_acrescimo_nao_oferecido", ConsequenciaDoDescumprimento.RetiraOAcrescimo)]
    [InlineData("banca_sem_modalidade_em_questao", ConsequenciaDoDescumprimento.RetiraAModalidade)]
    public void Painel_BancaComConsequenciaIncoerente_PendeOItemDaBanca(string item, ConsequenciaDoDescumprimento consequencia)
    {
        ProcessoSeletivo processo = ProcessoComBanca(consequencia);

        ItemDoPainel(processo.Declaracao(), processo, item).Ok.Should().BeFalse();
        ItemDoPainel(processo.Declaracao(), processo, "banca_retira_a_modalidade_sem_ampla_concorrencia").Ok.Should().BeTrue();
    }

    [Fact]
    public void Painel_BancaConforme_AprovaOsTresItensDaBanca()
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.EliminaDoProcesso);

        ItensDoPainel(processo.Declaracao(), processo).Where(static item => item.Codigo.StartsWith("banca_", StringComparison.Ordinal))
            .Should().HaveCount(3).And.OnlyContain(static item => item.Ok);
    }

    [Theory]
    [InlineData("etapas", "banca_acrescimo_nao_oferecido")]
    [InlineData("fada", "banca_acrescimo_nao_oferecido")]
    [InlineData("distribuicaoVagas", "banca_sem_modalidade_em_questao")]
    [InlineData("documentosExigidos", "banca_sem_modalidade_em_questao")]
    public void Painel_ParteNecessariaRecusada_MantemOItemDaBancaPendente(string recusada, string item)
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas(recusada), processo.Declaracao());

        ItemDoPainel(configuracao, processo, item).Ok.Should().BeFalse();
    }

    [Theory]
    [InlineData("fases[0].unidadeResponsavel")]
    [InlineData("etapas[0].itensDeAvaliacao")]
    public void Painel_ParteRecusadaForaDasBancas_AprovaOsItensDaBanca(string recusada)
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas(recusada), processo.Declaracao());

        ItensDoPainel(configuracao, processo).Where(static item => item.Codigo.StartsWith("banca_", StringComparison.Ordinal))
            .Should().HaveCount(3).And.OnlyContain(static item => item.Ok);
    }

    [Theory]
    [InlineData("fases[0].bancasRequeridas")]
    [InlineData("etapas[0].bancas")]
    public void Painel_ListaDeBancasRecusada_MantemOsItensDaBancaPendentes(string recusada)
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas(recusada), processo.Declaracao());

        ItensDoPainel(configuracao, processo).Where(static item => item.Codigo.StartsWith("banca_", StringComparison.Ordinal))
            .Should().HaveCount(3).And.OnlyContain(static item => !item.Ok);
    }

    [Fact]
    public void Painel_BancaQueRetiraAModalidadeSemAmpla_PendeOItemDeAmpla()
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.RetiraAModalidade);
        ConsequenciasDaRetiradaDaModalidadeTests.RetirarAmplaDaOferta(processo);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaDaEtapa(processo, "LB_PPI"), 0).Value!]).IsSuccess.Should().BeTrue();

        ItemDoPainel(processo.Declaracao(), processo, "banca_retira_a_modalidade_sem_ampla_concorrencia").Ok.Should().BeFalse();
    }

    [Theory]
    [InlineData("distribuicaoVagas")]
    [InlineData("documentosExigidos")]
    public void Painel_ParteRecusadaDaAmpla_MantemOItemDeAmplaPendente(string recusada)
    {
        ProcessoSeletivo processo = ProcessoComBanca(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], ComPartesRecusadas(recusada), processo.Declaracao());

        ItemDoPainel(configuracao, processo, "banca_retira_a_modalidade_sem_ampla_concorrencia").Ok.Should().BeFalse();
    }

    private static ItemConformidade ItemDoPainel(ConfiguracaoDeclarada configuracao, ProcessoSeletivo processo, string codigo) =>
        ItensDoPainel(configuracao, processo).Single(item => item.Codigo == codigo);

    private static IReadOnlyList<ItemConformidade> ItensDoPainel(ConfiguracaoDeclarada configuracao, ProcessoSeletivo processo) =>
        ItensDasConsequenciasDoDescumprimento.Avaliar(configuracao, CadastrosDeTeste.Para(processo).FatosDeModalidade);

    private static DocumentoExigido ExigenciaDaEtapa(ProcessoSeletivo processo, string codigo) =>
        new DocumentoExigidoBuilder()
            .ComMomento(new MomentoDaExigencia(processo.CronogramaFases.Single().Id, processo.Etapas.Single().Id, FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional,
                [CondicaoGatilho.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(codigo)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, null, []))
            .Construir();

    private static ProcessoSeletivo ProcessoComBanca(ConsequenciaDoDescumprimento consequencia)
    {
        ModalidadeSelecionada modalidade = NovaModalidadeComAcao("LB_PPI", NaturezaLegalModalidade.CotaReservada,
            ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(modalidade);
        BancaDaEtapa banca = BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista), consequenciaDaAusencia: consequencia);
        processo.Etapas.Single().DefinirBancas([banca]);
        return processo;
    }
}
