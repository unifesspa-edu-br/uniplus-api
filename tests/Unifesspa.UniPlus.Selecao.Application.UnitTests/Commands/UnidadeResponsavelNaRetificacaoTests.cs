namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os dois caminhos que congelam uma retificação resolvem de novo a unidade responsável de cada fase no
/// cadastro de agora: a que o cadastro excluiu depois do cronograma é recusada, e a renomeada é congelada com
/// a sigla e o nome atuais.
/// </summary>
public sealed class UnidadeResponsavelNaRetificacaoTests
{
    private static readonly UnidadeResponsavelSnapshot CepsRenomeada =
        new(UnidadesDeTeste.Ceps.OrigemId, "CEPS", "Centro de Processos Seletivos e Concursos");

    [Fact(DisplayName = "Retificar recusa a unidade responsável que o cadastro excluiu depois do cronograma")]
    public async Task Retificar_UnidadeExcluida_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComUnidadesVivas();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("fases[0].unidadeResponsavelId");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar congela a unidade responsável com a sigla e o nome de agora")]
    public async Task Retificar_UnidadeRenomeada_CongelaONomeNovo()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComUnidadesVivas(CepsRenomeada);

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.EntradaCanonicalizada!.Processo.CronogramaFases.Should().OnlyContain(f => f.UnidadeResponsavelNome == CepsRenomeada.Nome);
    }

    [Fact(DisplayName = "Fechar a retificação recusa a unidade responsável que o cadastro excluiu depois do cronograma")]
    public async Task Fechar_UnidadeExcluida_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.AbrirSessao();
        cenario.ComUnidadesVivas();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        eventos.Should().BeEmpty();
        cenario.Processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação congela a unidade responsável com a sigla e o nome de agora")]
    public async Task Fechar_UnidadeRenomeada_CongelaONomeNovo()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.AbrirSessao();
        cenario.ComUnidadesVivas(CepsRenomeada);

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.EntradaCanonicalizada!.Processo.CronogramaFases.Should().OnlyContain(f => f.UnidadeResponsavelNome == CepsRenomeada.Nome);
    }
}
