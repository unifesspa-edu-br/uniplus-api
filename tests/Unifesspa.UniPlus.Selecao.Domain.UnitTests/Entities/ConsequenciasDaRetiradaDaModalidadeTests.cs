namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;

public sealed class ConsequenciasDaRetiradaDaModalidadeTests
{
    [Theory]
    [InlineData("LB_PPI", false)]
    [InlineData("INEXISTENTE", true)]
    public void SemModalidade_CondicaoPorModalidade_ConfereAlcance(string codigo, bool recusada)
    {
        ProcessoSeletivo processo = ProcessoComExigencia(codigo);
        bool existe = CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDasExigencias(
            processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade).Any();
        existe.Should().Be(recusada);
    }

    [Fact]
    public void SemModalidade_CondicaoQueAlcancaTodas_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComExigencia("LB_PPI", "OUTRO_FATO");
        CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDasExigencias(
            processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade).Should().ContainSingle();
    }

    [Fact]
    public void SemAmpla_OfertaComAmpla_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComExigencia("LB_PPI");
        CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasExigencias(
            processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade).Should().BeEmpty();
    }

    [Theory]
    [InlineData("LB_PPI", false)]
    [InlineData("INEXISTENTE", true)]
    public void SemModalidade_Grupo_ConfereAlcance(string codigo, bool recusada)
    {
        ProcessoSeletivo processo = ProcessoComExigencia(codigo);
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(processo.DocumentosExigidos.Single(), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "RETIRA_A_MODALIDADE"),
            0, [BaseLegalDaComprovacao.Criar("Lei 12.711/2012", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!], [item]).Value!;
        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();
        CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDosGrupos(processo.Declaracao(),
            CadastrosDeTeste.Para(processo).FatosDeModalidade).Any().Should().Be(recusada);
    }

    [Theory]
    [InlineData("MODALIDADE", "LB_PPI", false)]
    [InlineData("MODALIDADE", "INEXISTENTE", true)]
    [InlineData("OUTRO_FATO", "LB_PPI", true)]
    public void SemModalidade_GrupoEmCadaLimite_ConfereAlcance(string fato, string codigo, bool recusada)
    {
        ProcessoSeletivo processo = ProcessoComExigencia(codigo, fato);
        Agrupar(processo);
        CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDosGrupos(processo.Declaracao(),
            CadastrosDeTeste.Para(processo).FatosDeModalidade).Any().Should().Be(recusada);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SemAmpla_ExigenciaEGrupo_ConfereOferta(bool comAmpla)
    {
        ProcessoSeletivo processo = ProcessoComExigencia("LB_PPI");
        if (!comAmpla)
        {
            RetirarAmplaDaOferta(processo);
        }
        Agrupar(processo);
        ConfiguracaoDeclarada declaracao = processo.Declaracao();
        FatosDeModalidade fatos = CadastrosDeTeste.Para(processo).FatosDeModalidade;
        CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasExigencias(declaracao, fatos).Any().Should().Be(!comAmpla);
        CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDosGrupos(declaracao, fatos).Any().Should().Be(!comAmpla);
        CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasModalidades(declaracao).Any().Should().Be(!comAmpla);
    }

    [Fact]
    public void Causas_FamiliasDiferentes_AcumulamNaOrdemDoAssunto()
    {
        ProcessoSeletivo processo = ProcessoComExigencia("LB_PPI", "OUTRO_FATO");
        RetirarAmplaDaOferta(processo);
        AdicionarExigenciaDeAcrescimo(processo);
        FieldError[] causas = [.. CoerenciaDaConsequenciaDoDescumprimento.Causas(processo.Declaracao(), CadastrosDeTeste.Para(processo).FatosDeModalidade, CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas))];
        causas.Select(static causa => causa.Error.Code).Should().Equal(
            "ConsequenciasDoDescumprimento.AcrescimoNaoOferecido",
            "ConsequenciasDoDescumprimento.SemModalidadeEmQuestao",
            "ConsequenciasDoDescumprimento.RetiraAModalidadeSemAmplaConcorrencia",
            "ConsequenciasDoDescumprimento.RetiraAModalidadeSemAmplaConcorrencia");
    }

    [Fact]
    public void SemAmplaDasModalidades_DuasModalidadesQueRetiramNaMesmaOferta_RecusaUmaVezPorOferta()
    {
        ProcessoSeletivo processo = ProcessoComExigencia("LB_PPI");
        RetirarAmplaDaOferta(processo, acaoDaOutraModalidade: "RETIRA_A_MODALIDADE");
        FieldError[] causas = [.. CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasModalidades(processo.Declaracao())];
        causas.Select(static causa => causa.Field).Should().Equal("distribuicaoVagas[0].modalidades");
    }

    [Fact]
    public void PainelESuasCausas_SemAmpla_ApontamExigenciaEGrupo()
    {
        ProcessoSeletivo processo = ProcessoComExigencia("LB_PPI");
        RetirarAmplaDaOferta(processo);
        Agrupar(processo);
        ConfiguracaoDeclarada declaracao = processo.Declaracao();
        CadastrosDaConfiguracao cadastros = CadastrosDeTeste.Para(processo);
        string[] pendentes = [.. ChecklistDeConformidade.AvaliarConformidade(declaracao, cadastros)
            .Where(static item => item.Codigo.EndsWith("retira_a_modalidade_sem_ampla_concorrencia", StringComparison.Ordinal) && !item.Ok)
            .Select(static item => item.Codigo)];
        pendentes.Should().Equal("exigencia_retira_a_modalidade_sem_ampla_concorrencia", "grupo_retira_a_modalidade_sem_ampla_concorrencia", "modalidade_retira_a_modalidade_sem_ampla_concorrencia");
        CoerenciaDaConsequenciaDoDescumprimento.Causas(declaracao, cadastros.FatosDeModalidade, CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas))
            .Count(static causa => causa.Error.Code == "ConsequenciasDoDescumprimento.RetiraAModalidadeSemAmplaConcorrencia").Should().Be(3);
    }

    private static void AdicionarExigenciaDeAcrescimo(ProcessoSeletivo processo)
    {
        DocumentoExigido retirada = processo.DocumentosExigidos.Single();
        DocumentoExigido acrescimo = ExigenciaCondicionalPorModalidadeComConsequencia(
            processo.CronogramaFases.Single().Id, "OUTRO_DOC", "Outro documento", "PESSOAL", "LB_PPI", "RETIRA_O_ACRESCIMO");
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(retirada, 0).Value!,
            RegraDeComprovacao.CriarRegraDeDocumento(acrescimo, 1).Value!]).IsSuccess.Should().BeTrue();
    }

    private static void Agrupar(ProcessoSeletivo processo)
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(processo.DocumentosExigidos.Single(), 0).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "RETIRA_A_MODALIDADE"),
            0, [BaseLegalDaComprovacao.Criar("Lei 12.711/2012", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!], [item]).Value!;
        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();
    }

    internal static void RetirarAmplaDaOferta(ProcessoSeletivo processo, string? acaoDaOutraModalidade = null)
    {
        ModalidadeSelecionada modalidade = NovaModalidadeComAcao("LB_PPI", NaturezaLegalModalidade.AcaoAfirmativa,
            ComposicaoVagasModalidade.SuplementarAoTotal, "RETIRA_A_MODALIDADE");
        ModalidadeNoCadastro outra = ModalidadesNoCadastroDeTeste.Da("OUTRA", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { AcaoQuandoIndeferido = acaoDaOutraModalidade };
        ModalidadeSelecionada restante = ModalidadeSelecionada.Criar(outra, 40).Value!;
        var declarada = new DistribuicaoDeVagasDeclarada
        {
            VagasOfertadasBase = 50,
            PercentualDaReservaLegal = 1m,
            RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", new string('a', 64)),
            Modalidades = [modalidade, restante],
        };
        Result<ConfiguracaoDistribuicaoVagas> oferta = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(), declarada, new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });
        oferta.IsSuccess.Should().BeTrue("{0}", string.Join(", ", oferta.Errors));
        processo.DefinirDistribuicaoVagas([oferta.Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static ProcessoSeletivo ProcessoComExigencia(string codigo, string fato = "MODALIDADE")
    {
        ModalidadeSelecionada modalidade = NovaModalidadeComAcao("LB_PPI", NaturezaLegalModalidade.CotaReservada,
            ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(modalidade);
        DocumentoExigido exigencia = ExigenciaCondicionalPorModalidadeComConsequencia(
            processo.CronogramaFases.Single().Id, "DOC", "Documento", "PESSOAL", codigo, "RETIRA_A_MODALIDADE", fato);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();
        return processo;
    }
}
