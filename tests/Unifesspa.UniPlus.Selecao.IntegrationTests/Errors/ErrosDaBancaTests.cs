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
    [InlineData("Banca.AtoIndividualSemVotacao", "uniplus.selecao.banca.ato_individual_sem_votacao")]
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
    [InlineData("Banca.RecorteEmBancaDeRecursos", "uniplus.selecao.banca.recorte_em_banca_de_recursos")]
    [InlineData("Cronograma.RecursoSemBanca", "uniplus.selecao.cronograma.recurso_sem_banca")]
    public void Recusa_MapeadaPara422ComOCodigoEstavel(string codigoDoDominio, string codigoNoFio)
    {
        bool mapeada = Mapper.TryGetMapping(codigoDoDominio, out DomainErrorMapping? mapeamento);

        mapeada.Should().BeTrue();
        mapeamento!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        mapeamento.Code.Should().Be(codigoNoFio);
        mapeamento.Title.Should().NotBeNullOrWhiteSpace();
        Mapper.GetProblemTypeUri(codigoNoFio).Should().Be(MapperDoModuloDeTeste.BaseUriDeErros + codigoNoFio);
    }
}
