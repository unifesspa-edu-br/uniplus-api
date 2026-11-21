namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A causa que aponta fase, etapa ou recurso pela posição canônica leva também o identificador estável do item, para o cliente achar
/// o item certo depois de outra gravação mudar as posições; o item que não existe não aponta outro.
/// </summary>
public sealed class IdentificacaoDosItensDaCausaTests
{
    private static readonly DomainError Erro = new("Teste.Recusa", "Mensagem.");

    private static (ConfiguracaoDeclarada Configuracao, EtapaProcesso Etapa) ProcessoComDoisRecursos()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([RecursoModelo.NaCiencia(), RecursoModelo.AoAto(etapa.Produtos.Single().Id)]).IsSuccess.Should().BeTrue();

        return (processo.Declaracao(), etapa);
    }

    private static FieldError[] Identificar(ConfiguracaoDeclarada configuracao, params string?[] campos) =>
        [.. IdentificacaoDosItensDaCausa.Identificar([.. campos.Select(static campo => new FieldError(campo, Erro))], configuracao)];

    [Fact(DisplayName = "A causa em uma fase leva o código do tipo de fase dela")]
    public void Identificar_CausaNaFase_LevaOCodigoDaFase()
    {
        (ConfiguracaoDeclarada configuracao, _) = ProcessoComDoisRecursos();

        FieldError causa = Identificar(configuracao, "fases[0].fim").Single();

        causa.Identificadores.Should().Equal(new IdentificadorDoItem("fases", configuracao.Cronograma.CronogramaFases[0].Codigo));
        causa.Field.Should().Be("fases[0].fim", "o campo não muda: quem o converte em ponteiro é outra etapa");
    }

    [Fact(DisplayName = "A causa em uma etapa leva o identificador da etapa")]
    public void Identificar_CausaNaEtapa_LevaOIdentificadorDaEtapa()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();

        Identificar(configuracao, "etapas[0].produtos[0].papel").Single().Identificadores.Should().Equal(
            new IdentificadorDoItem("etapas", etapa.Id.ToString("D")));
    }

    [Fact(DisplayName = "A causa em um recurso da etapa leva o identificador da etapa e o do recurso, na ordem do caminho e na ordem canônica dos recursos")]
    public void Identificar_CausaNoRecurso_LevaEtapaERecursoNaOrdemCanonica()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();
        RecursoDaEtapa segundoNaOrdemCanonica = etapa.Recursos.ElementAt(1);

        FieldError causa = Identificar(configuracao, "etapas[0].recursos[1].regraCodigo").Single();

        causa.Identificadores.Should().Equal(
            new IdentificadorDoItem("etapas", etapa.Id.ToString("D")),
            new IdentificadorDoItem("recursos", segundoNaOrdemCanonica.Id.ToString("D")));
        etapa.Recursos.ElementAt(0).Id.Should().NotBe(segundoNaOrdemCanonica.Id);
    }

    [Fact(DisplayName = "O índice de recurso depois da última posição deixa só a etapa, e o de fase ou de etapa inexistente não leva identificador")]
    public void Identificar_ItemInexistente_NaoApontaOutroItem()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();

        FieldError[] causas = Identificar(configuracao, "etapas[0].recursos[2].regraCodigo", "fases[3].fim", "etapas[5].nome");

        causas[0].Identificadores.Should().Equal(new IdentificadorDoItem("etapas", etapa.Id.ToString("D")));
        causas[1].Identificadores.Should().BeNull();
        causas[2].Identificadores.Should().BeNull();
    }

    [Theory(DisplayName = "A causa sem campo, de outro bloco, que já é ponteiro ou com índice que não é posição fica como está")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("identificacao.nome")]
    [InlineData("fases")]
    [InlineData("fases[x].fim")]
    [InlineData("fases[].fim")]
    [InlineData("fases[0")]
    [InlineData("/cronograma/fases/0/fim")]
    public void Identificar_CausaQueNaoApontaItemDeLista_FicaComoEstava(string? campo)
    {
        (ConfiguracaoDeclarada configuracao, _) = ProcessoComDoisRecursos();

        FieldError causa = Identificar(configuracao, campo).Single();

        causa.Should().Be(new FieldError(campo, Erro));
    }

    [Fact(DisplayName = "Cada causa recebe o identificador do próprio item, na mesma ordem, e as demais informações ficam como estavam")]
    public void Identificar_VariasCausas_PreservaAOrdemEOsDemaisMembros()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();
        FieldError[] entrada = [new("etapas[0].nome", Erro, "origem-a"), new("fases[0].fim", Erro, "origem-b"), new(null, Erro)];

        IReadOnlyList<FieldError> saida = IdentificacaoDosItensDaCausa.Identificar(entrada, configuracao);

        saida.Select(static c => (c.Field, c.Origem)).Should().Equal(("etapas[0].nome", "origem-a"), ("fases[0].fim", "origem-b"), (null, null));
        saida[0].Identificadores!.Single().Identificador.Should().Be(etapa.Id.ToString("D"));
        saida[2].Identificadores.Should().BeNull();
    }

    [Fact(DisplayName = "As causas e a configuração são obrigatórias")]
    public void Identificar_ArgumentoNulo_Lanca()
    {
        (ConfiguracaoDeclarada configuracao, _) = ProcessoComDoisRecursos();

        FluentActions.Invoking(() => IdentificacaoDosItensDaCausa.Identificar(null!, configuracao)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => IdentificacaoDosItensDaCausa.Identificar([], null!)).Should().Throw<ArgumentNullException>();
    }
}
