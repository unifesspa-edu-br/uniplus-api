namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Xunit;

/// <summary>
/// Cada recusa da composição, da regra de deliberação e da exigência de banca chega ao cliente com o
/// status 422 e o código estável do módulo, que o <c>type</c> do problema resolve na página do catálogo.
/// </summary>
public sealed class ErrosDaBancaTests
{
    private static readonly IDomainErrorMapper Mapper = MapperDoModuloDeTeste.Criar();

    [Theory(DisplayName = "A recusa da banca é mapeada para 422 com o código estável do módulo")]
    [InlineData("Banca.ModoDeDeliberacaoInvalido", "uniplus.selecao.banca.modo_de_deliberacao_invalido")]
    [InlineData("Banca.IntegrantesInvalidos", "uniplus.selecao.banca.integrantes_invalidos")]
    [InlineData("Banca.SuplentesInvalidos", "uniplus.selecao.banca.suplentes_invalidos")]
    [InlineData("Banca.VotacaoDoRecursoIncompleta", "uniplus.selecao.banca.votacao_do_recurso_incompleta")]
    [InlineData("Banca.QuorumForaDoIntervalo", "uniplus.selecao.banca.quorum_fora_do_intervalo")]
    [InlineData("Banca.QuorumIncoerente", "uniplus.selecao.banca.quorum_incoerente")]
    [InlineData("Banca.CriterioObrigatorio", "uniplus.selecao.banca.criterio_obrigatorio")]
    [InlineData("Banca.EncerramentoObrigatorio", "uniplus.selecao.banca.encerramento_obrigatorio")]
    [InlineData("Banca.DesempateObrigatorio", "uniplus.selecao.banca.desempate_obrigatorio")]
    [InlineData("Banca.DesempateNaoSeAplica", "uniplus.selecao.banca.desempate_nao_se_aplica")]
    [InlineData("Banca.SemUnanimidadeObrigatoria", "uniplus.selecao.banca.sem_unanimidade_obrigatoria")]
    [InlineData("Banca.SemUnanimidadeNaoSeAplica", "uniplus.selecao.banca.sem_unanimidade_nao_se_aplica")]
    [InlineData("Banca.RegraDeDeliberacaoObrigatoria", "uniplus.selecao.banca.regra_de_deliberacao_obrigatoria")]
    [InlineData("Banca.IntegrantesAbaixoDoMinimo", "uniplus.selecao.banca.integrantes_abaixo_do_minimo")]
    [InlineData("EtapaProcesso.RegraDeConsolidacaoNaoResolvida", "uniplus.selecao.etapa_processo.regra_de_consolidacao_nao_resolvida")]
    [InlineData("Banca.TipoNaoAdmiteInstanciaSuperior", "uniplus.selecao.banca.tipo_nao_admite_instancia_superior")]
    [InlineData("Banca.CandidatosAtendidosEmBancaDeRecursos", "uniplus.selecao.banca.candidatos_atendidos_em_banca_de_recursos")]
    [InlineData("Cronograma.RecursoSemBanca", "uniplus.selecao.cronograma.recurso_sem_banca")]
    [InlineData("Banca.FaseSemBancaDaSegundaInstancia", "uniplus.selecao.banca.fase_sem_banca_da_segunda_instancia")]
    [InlineData("Banca.EtapaSemBancaDaSegundaInstancia", "uniplus.selecao.banca.etapa_sem_banca_da_segunda_instancia")]
    [InlineData("Banca.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar", "uniplus.selecao.banca.fase_com_banca_da_segunda_instancia_sem_recurso_para_julgar")]
    [InlineData("Banca.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar", "uniplus.selecao.banca.etapa_com_banca_da_segunda_instancia_sem_recurso_para_julgar")]
    [InlineData("Banca.FaseComMaisDeUmaBancaDaSegundaInstancia", "uniplus.selecao.banca.fase_com_mais_de_uma_banca_da_segunda_instancia")]
    [InlineData("Banca.EtapaComMaisDeUmaBancaDaSegundaInstancia", "uniplus.selecao.banca.etapa_com_mais_de_uma_banca_da_segunda_instancia")]
    public void Recusa_MapeadaPara422ComOCodigoEstavel(string codigoDoDominio, string codigoNoFio)
    {
        bool mapeada = Mapper.TryGetMapping(codigoDoDominio, out DomainErrorMapping? mapeamento);

        mapeada.Should().BeTrue();
        mapeamento!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        mapeamento.Code.Should().Be(codigoNoFio);
        mapeamento.Title.Should().NotBeNullOrWhiteSpace();
        Mapper.GetProblemTypeUri(codigoNoFio).Should().Be(MapperDoModuloDeTeste.BaseUriDeErros + codigoNoFio);
    }

    [Theory]
    [InlineData("Banca.FaseSemBancaDaSegundaInstancia", "O recurso admite instância superior, e a fase não prevê banca que o julgue em 2ª instância")]
    [InlineData("Banca.EtapaSemBancaDaSegundaInstancia", "O recurso admite instância superior, e nem a etapa nem a fase preveem banca que o julgue em 2ª instância")]
    [InlineData("Banca.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar", "A fase prevê banca que julga o recurso em 2ª instância, e não há recurso para ela julgar")]
    [InlineData("Banca.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar", "A etapa prevê banca que julga o recurso em 2ª instância, e nenhum recurso da etapa admite instância superior")]
    [InlineData("Banca.FaseComMaisDeUmaBancaDaSegundaInstancia", "A fase prevê mais de uma banca que julga o recurso em 2ª instância")]
    [InlineData("Banca.EtapaComMaisDeUmaBancaDaSegundaInstancia", "A etapa prevê mais de uma banca que julga o recurso em 2ª instância")]
    public void RecusaDaSegundaInstancia_TemOTituloDoContrato(string codigoDoDominio, string titulo)
    {
        Mapper.TryGetMapping(codigoDoDominio, out DomainErrorMapping? mapeamento).Should().BeTrue();

        mapeamento!.Title.Should().Be(titulo);
    }
}
