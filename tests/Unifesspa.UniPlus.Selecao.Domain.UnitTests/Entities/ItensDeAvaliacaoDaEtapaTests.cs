namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Os itens de avaliação e a escala de atendimento da etapa: o que a gravação recusa, em rascunho
/// também, e a identidade estável do item.
/// </summary>
public sealed class ItensDeAvaliacaoDaEtapaTests
{
    private static TipoEtapaSnapshot Tipo(bool doEnem = false) =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ENTREVISTA", "Entrevista", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: doEnem).Value!;

    private static readonly NivelDeAtendimento[] EscalaCompleta =
    [
        new("Atendeu", 1m),
        new("Atendeu parcialmente", 0.5m),
        new("Não atendeu", 0m),
    ];

    private static Result<EtapaProcesso> Criar(
        IReadOnlyList<ItemDeAvaliacaoDeclarado>? itens,
        IReadOnlyList<NivelDeAtendimento>? escala = null,
        FormaDeAvaliacao? forma = FormaDeAvaliacao.Humana,
        decimal? passo = null,
        bool doEnem = false) =>
        EtapaProcesso.Criar(
            "Entrevista", CaraterEtapa.Classificatoria, Tipo(doEnem), peso: 1m, notaMaxima: 10m, passoDoLancamento: passo,
            formaDeAvaliacao: forma, itensDeAvaliacao: itens, escalaDeAtendimento: escala);

    private static ItemDeAvaliacaoDeclarado Item(string rotulo, decimal pontuacao, Guid? id = null) => new(id, rotulo, pontuacao);

    private static string[] Codigos(Result<EtapaProcesso> resultado) =>
        [.. resultado.Errors.Select(static e => e.Error.Code)];

    [Fact(DisplayName = "Os itens e a escala válidos são gravados, e a escala sai do nível que mais vale ao que menos vale")]
    public void Criar_ItensEEscalaValidos_Grava()
    {
        EtapaProcesso etapa = Criar([Item("Clareza", 4m), Item("Domínio do tema", 6m)], EscalaCompleta.Reverse().ToArray()).Value!;

        etapa.ItensDeAvaliacao.Select(static i => i.Rotulo).Should().Equal("Clareza", "Domínio do tema");
        etapa.EscalaDeAtendimento!.Select(static n => n.Fracao).Should().Equal(1m, 0.5m, 0m);
    }

    [Fact(DisplayName = "O rótulo do item é obrigatório")]
    public void Criar_ItemSemRotulo_Recusa()
    {
        Result<EtapaProcesso> resultado = Criar([Item("  ", 10m)]);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("itensDeAvaliacao[0].rotulo");
        Codigos(resultado).Should().Equal(ItemDeAvaliacao.RotuloInvalido);
    }

    [Fact(DisplayName = "Dois itens com o mesmo rótulo, mesmo em forma Unicode diferente, são recusados")]
    public void Criar_RotuloDuplicadoEmFormaUnicodeDiferente_Recusa()
    {
        Result<EtapaProcesso> resultado = Criar([Item("Redação", 5m), Item("Redac\u0327a\u0303o", 5m)]);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("itensDeAvaliacao[1].rotulo");
        Codigos(resultado).Should().Equal(ItemDeAvaliacao.RotuloDuplicado);
    }

    [Fact(DisplayName = "O mesmo id de item informado duas vezes na etapa é recusado")]
    public void Criar_IdDeItemRepetido_Recusa()
    {
        Guid id = Guid.CreateVersion7();

        Codigos(Criar([Item("A", 5m, id), Item("B", 5m, id)])).Should().Equal(ItemDeAvaliacao.IdDuplicado);
    }

    [Theory(DisplayName = "A pontuação do item deve ser maior que zero")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_PontuacaoNaoPositiva_Recusa(int pontuacao) =>
        Codigos(Criar([Item("A", pontuacao)])).Should().Equal(ItemDeAvaliacao.PontuacaoMaximaInvalida);

    [Fact(DisplayName = "Com passo, a pontuação do item deve ser múltipla dele; sem passo, qualquer valor positivo serve")]
    public void Criar_PontuacaoForaDoPasso_Recusa()
    {
        Codigos(Criar([Item("A", 2.3m), Item("B", 7.5m)], passo: 0.5m)).Should().Equal(ItemDeAvaliacao.PontuacaoForaDoPasso);
        Criar([Item("A", 2.3m), Item("B", 7.7m)]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Os erros de vários itens acumulam em uma só recusa")]
    public void Criar_VariosItensInvalidos_AcumulaOsErros()
    {
        Result<EtapaProcesso> resultado = Criar([Item("", 0m), Item("B", 5m), Item("B", 5m)]);

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(
            "itensDeAvaliacao[0].rotulo", "itensDeAvaliacao[0].pontuacaoMaxima", "itensDeAvaliacao[2].rotulo");
    }

    [Fact(DisplayName = "A soma das pontuações diferente da nota máxima não recusa a gravação: é pendência da publicação")]
    public void Criar_SomaDiferenteDaNotaMaxima_Grava() =>
        Criar([Item("A", 3m), Item("B", 3m)]).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "A escala exige um nível que valha a pontuação inteira")]
    public void Criar_EscalaSemNivelCompleto_Recusa() =>
        Codigos(Criar([Item("A", 10m)], [new("Atendeu parcialmente", 0.5m), new("Não atendeu", 0m)]))
            .Should().Equal(NiveisDeAtendimento.SemNivelCompleto);

    [Theory(DisplayName = "A fração do nível fica entre 0 e 1")]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Criar_FracaoForaDoIntervalo_Recusa(double fracao)
    {
        Result<EtapaProcesso> resultado = Criar([Item("A", 10m)], [new("Atendeu", 1m), new("Excedeu", (decimal)fracao)]);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("escalaDeAtendimento[1].fracao");
        Codigos(resultado).Should().Equal(NiveisDeAtendimento.FracaoInvalida);
    }

    [Fact(DisplayName = "O rótulo do nível é obrigatório e não se repete na escala")]
    public void Criar_NiveisComRotuloInvalido_Recusa() =>
        Codigos(Criar([Item("A", 10m)], [new("Atendeu", 1m), new(" ", 0.5m), new("Atendeu", 0m)]))
            .Should().BeEquivalentTo(NiveisDeAtendimento.RotuloInvalido, NiveisDeAtendimento.NivelDuplicado);

    [Fact(DisplayName = "A escala só vale com itens")]
    public void Criar_EscalaSemItens_Recusa() =>
        Codigos(Criar([], EscalaCompleta)).Should().Equal(EtapaProcesso.EscalaSemItens);

    [Fact(DisplayName = "A escala nunca vale na forma objetiva")]
    public void Criar_EscalaNaFormaObjetiva_Recusa() =>
        Codigos(Criar([Item("A", 10m)], EscalaCompleta, FormaDeAvaliacao.Objetiva)).Should().Equal(EtapaProcesso.EscalaNaFormaObjetiva);

    [Fact(DisplayName = "Em rascunho, a escala sem forma declarada é gravada: a falta da forma é do gate")]
    public void Criar_EscalaSemForma_Grava() =>
        Criar([Item("A", 10m)], EscalaCompleta, forma: null).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Com escala, o passo do lançamento é recusado")]
    public void Criar_PassoComEscala_Recusa()
    {
        Result<EtapaProcesso> resultado = Criar([Item("A", 10m)], EscalaCompleta, passo: 0.5m);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("passoDoLancamento");
        Codigos(resultado).Should().Equal(EtapaProcesso.PassoComEscalaDeAtendimento);
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa itens e escala, cada um no seu campo")]
    public void Criar_ItensEEscalaNaEtapaDoEnem_Recusa()
    {
        Result<EtapaProcesso> resultado = Criar([Item("A", 10m)], EscalaCompleta, forma: null, doEnem: true);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itensDeAvaliacao", EtapaProcesso.ItensNaEtapaDoEnem),
            ("escalaDeAtendimento", EtapaProcesso.ItensNaEtapaDoEnem),
        ]);
    }

    [Fact(DisplayName = "O item com o id de um existente é a mesma linha: o rótulo muda e o id continua")]
    public void AtualizarDados_ItemComIdExistente_PreservaOId()
    {
        EtapaProcesso etapa = Criar([Item("Clareza", 4m), Item("Domínio", 6m)]).Value!;
        Guid idDaClareza = etapa.ItensDeAvaliacao.Single(static i => i.Rotulo == "Clareza").Id;

        etapa.AtualizarDados(
            "Entrevista", CaraterEtapa.Classificatoria, Tipo(), 1m, null, null, notaMaxima: 10m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana,
            itensDeAvaliacao: [Item("Objetividade", 5m, idDaClareza), Item("Domínio", 5m)]).IsSuccess.Should().BeTrue();

        etapa.ItensDeAvaliacao.Should().Contain(i => i.Id == idDaClareza && i.Rotulo == "Objetividade" && i.PontuacaoMaxima == 5m);
        etapa.ItensDeAvaliacao.Should().HaveCount(2);
    }

    [Fact(DisplayName = "O item sem id, ou com um que a etapa não conhece, é um item novo e o que saiu da lista é removido")]
    public void AtualizarDados_ItemSemIdOuComIdDesconhecido_CriaItemNovo()
    {
        EtapaProcesso etapa = Criar([Item("Clareza", 4m), Item("Domínio", 6m)]).Value!;
        HashSet<Guid> antes = [.. etapa.ItensDeAvaliacao.Select(static i => i.Id)];
        Guid desconhecido = Guid.CreateVersion7();

        etapa.AtualizarDados(
            "Entrevista", CaraterEtapa.Classificatoria, Tipo(), 1m, null, null, notaMaxima: 10m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana,
            itensDeAvaliacao: [Item("Postura", 5m), Item("Fluência", 5m, desconhecido)]).IsSuccess.Should().BeTrue();

        etapa.ItensDeAvaliacao.Select(static i => i.Id).Should().NotIntersectWith(antes).And.NotContain(desconhecido);
        etapa.ItensDeAvaliacao.Should().HaveCount(2);
    }

    [Fact(DisplayName = "A recusa na atualização deixa os itens como estavam")]
    public void AtualizarDados_Recusada_NaoAlteraOsItens()
    {
        EtapaProcesso etapa = Criar([Item("Clareza", 10m)]).Value!;

        etapa.AtualizarDados(
            "Entrevista", CaraterEtapa.Classificatoria, Tipo(), 1m, null, null, notaMaxima: 10m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana,
            itensDeAvaliacao: [Item("", 10m)]).IsFailure.Should().BeTrue();

        etapa.ItensDeAvaliacao.Should().ContainSingle().Which.Rotulo.Should().Be("Clareza");
    }
}
