namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Caracterização das recusas de <see cref="EtapaProcesso"/> na declaração dos dados: a equivalência
/// entre <c>Criar</c> e <c>AtualizarDados</c>, os limites de peso, nota mínima e escala por caráter,
/// a etapa de nota do ENEM e a escala de atendimento com elemento nulo.
/// </summary>
public sealed class EtapaProcessoRecusasTests
{
    private sealed record EntradaDaEtapa
    {
        public string Nome { get; init; } = "Entrevista";
        public CaraterEtapa Carater { get; init; } = CaraterEtapa.Ambas;
        public bool DoEnem { get; init; }
        public decimal? Peso { get; init; }
        public decimal? NotaMinima { get; init; }
        public int? Ordem { get; init; }
        public decimal? NotaMaxima { get; init; }
        public decimal? Passo { get; init; }
        public FormaDeAvaliacao? Forma { get; init; }
        public ReferenciaRegra? Regra { get; init; }
        public IReadOnlyList<ItemDeAvaliacaoDeclarado>? Itens { get; init; }
        public IReadOnlyList<NivelDeAtendimento>? Escala { get; init; }
    }

    private static TipoEtapaSnapshot Tipo(bool doEnem = false) => TipoEtapaSnapshot.Criar(
        Guid.CreateVersion7(), "ENTREVISTA", "Entrevista",
        admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: doEnem).Value!;

    private static ReferenciaRegra Regra(string codigo = RegraConsolidacaoCodigo.AvaliacaoUnica) =>
        ReferenciaRegra.De(codigo, "v1", new string('c', 64));

    private static ItemDeAvaliacaoDeclarado Item(string rotulo, decimal pontuacao) => new(null, rotulo, pontuacao);

    private static Result<EtapaProcesso> Criar(EntradaDaEtapa e) => EtapaProcesso.Criar(
        new IdentificacaoDaEtapa(Nome: e.Nome, Carater: e.Carater, TipoEtapa: Tipo(e.DoEnem), Ordem: e.Ordem, FaseCodigo: null),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: e.Peso, NotaMinima: e.NotaMinima, NotaMaxima: e.NotaMaxima, PassoDoLancamento: e.Passo, FormaDeAvaliacao: e.Forma, RegraConsolidacao: e.Regra, EscalaDeAtendimento: e.Escala),
        e.Itens);

    private static Result Atualizar(EntradaDaEtapa e)
    {
        EtapaProcesso existente = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Etapa existente", Carater: CaraterEtapa.Classificatoria, TipoEtapa: Tipo(), Ordem: null, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        return existente.AtualizarDados(
            new IdentificacaoDaEtapa(Nome: e.Nome, Carater: e.Carater, TipoEtapa: Tipo(e.DoEnem), Ordem: e.Ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: e.Peso, NotaMinima: e.NotaMinima, NotaMaxima: e.NotaMaxima, PassoDoLancamento: e.Passo, FormaDeAvaliacao: e.Forma, RegraConsolidacao: e.Regra, EscalaDeAtendimento: e.Escala),
            e.Itens);
    }

    private static (string? Campo, string Codigo)[] Pares(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static e => (e.Field, e.Error.Code))];

    private static List<FieldError> FormaBasica(
        CaraterEtapa carater, decimal? peso = null, decimal? notaMinima = null, decimal? notaMaxima = null, decimal? passo = null) =>
        ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            "Entrevista", carater, ordem: 1,
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: notaMinima, NotaMaxima: notaMaxima, PassoDoLancamento: passo, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    // ── Criar e AtualizarDados recusam igual ──

    private static EntradaDaEtapa EntradaComRecusasDaFormaEDaAvaliacao() => new()
    {
        Nome = string.Empty,
        Carater = CaraterEtapa.Nenhum,
        Peso = 0m,
        NotaMinima = -1m,
        Ordem = 0,
        NotaMaxima = 0m,
        Passo = -1m,
        Forma = FormaDeAvaliacao.Objetiva,
        Itens = [Item("Clareza", 5m)],
        Escala = [new("Atendeu", 1m)],
    };

    private static readonly (string? Campo, string Codigo)[] RecusasDaFormaEDaAvaliacaoEsperadas =
    [
        ("nome", "EtapaProcesso.NomeObrigatorio"),
        ("carater", "EtapaProcesso.CaraterObrigatorio"),
        ("peso", "EtapaProcesso.PesoInvalido"),
        ("notaMinima", "EtapaProcesso.NotaMinimaInvalida"),
        ("notaMaxima", EtapaProcessoErrorCodes.NotaMaximaInvalida),
        ("passoDoLancamento", EtapaProcessoErrorCodes.PassoDoLancamentoInvalido),
        ("ordem", "EtapaProcesso.OrdemInvalida"),
        ("passoDoLancamento", EtapaProcessoErrorCodes.PassoNaFormaObjetiva),
        ("escalaDeAtendimento", EtapaProcessoErrorCodes.EscalaNaFormaObjetiva),
        ("passoDoLancamento", EtapaProcessoErrorCodes.PassoComEscalaDeAtendimento),
    ];

    [Fact(DisplayName = "Criar recusa a forma e a avaliação nesta ordem: nome, caráter, peso, nota, escala, ordem, forma e escala de atendimento")]
    public void Criar_RecusasDaFormaEDaAvaliacao_SeguemAOrdemDeclarada()
    {
        Result<EtapaProcesso> resultado = Criar(EntradaComRecusasDaFormaEDaAvaliacao());

        Pares(resultado.Errors).Should().Equal(RecusasDaFormaEDaAvaliacaoEsperadas);
    }

    [Fact(DisplayName = "AtualizarDados recusa exatamente o que Criar recusa, na mesma ordem e com as mesmas mensagens")]
    public void AtualizarDados_RecusasDaFormaEDaAvaliacao_SaoAsDeCriar()
    {
        EntradaDaEtapa entrada = EntradaComRecusasDaFormaEDaAvaliacao();

        Atualizar(entrada).Errors.Should().Equal(Criar(entrada).Errors);
    }

    private static EntradaDaEtapa EntradaDoEnemComAvaliacaoDeclarada() => new()
    {
        DoEnem = true,
        Forma = FormaDeAvaliacao.Humana,
        Regra = Regra(),
        Itens = [Item("Clareza", 10m)],
        Escala = [new("Atendeu", 1m)],
    };

    [Fact(DisplayName = "Criar e AtualizarDados recusam igual a etapa de nota do ENEM")]
    public void CriarEAtualizarDados_EtapaDoEnemComAvaliacaoDeclarada_RecusamIgual()
    {
        EntradaDaEtapa entrada = EntradaDoEnemComAvaliacaoDeclarada();

        Pares(Criar(entrada).Errors).Should().Equal(
            ("formaDeAvaliacao", EtapaProcessoErrorCodes.AvaliacaoNaEtapaDoEnem),
            ("regraConsolidacao", EtapaProcessoErrorCodes.AvaliacaoNaEtapaDoEnem),
            ("itensDeAvaliacao", EtapaProcessoErrorCodes.ItensNaEtapaDoEnem),
            ("escalaDeAtendimento", EtapaProcessoErrorCodes.ItensNaEtapaDoEnem));
        Atualizar(entrada).Errors.Should().Equal(Criar(entrada).Errors);
    }

    // ── Peso e nota mínima por caráter ──

    [Theory(DisplayName = "O peso é recusado só na etapa apenas eliminatória; sem caráter definido o peso não é recusado")]
    [InlineData(CaraterEtapa.Classificatoria, false)]
    [InlineData(CaraterEtapa.Ambas, false)]
    [InlineData(CaraterEtapa.Eliminatoria, true)]
    [InlineData(CaraterEtapa.Nenhum, false)]
    [InlineData((CaraterEtapa)4, false)]
    public void ValidarFormaBasica_PesoPorCarater_RecusaSoNaEliminatoria(CaraterEtapa carater, bool recusa)
    {
        List<FieldError> erros = FormaBasica(carater, peso: 2m);

        erros.Exists(static e => e.Field == "peso").Should().Be(recusa);
    }

    [Theory(DisplayName = "A nota mínima é recusada só na etapa apenas classificatória; sem caráter definido ela não é recusada")]
    [InlineData(CaraterEtapa.Classificatoria, true)]
    [InlineData(CaraterEtapa.Ambas, false)]
    [InlineData(CaraterEtapa.Eliminatoria, false)]
    [InlineData(CaraterEtapa.Nenhum, false)]
    [InlineData((CaraterEtapa)4, false)]
    public void ValidarFormaBasica_NotaMinimaPorCarater_RecusaSoNaClassificatoria(CaraterEtapa carater, bool recusa)
    {
        List<FieldError> erros = FormaBasica(carater, notaMinima: 5m);

        erros.Exists(static e => e.Field == "notaMinima").Should().Be(recusa);
    }

    [Theory(DisplayName = "Peso e nota mínima juntos em caráter indefinido recusam só o caráter")]
    [InlineData(CaraterEtapa.Nenhum)]
    [InlineData((CaraterEtapa)4)]
    public void ValidarFormaBasica_CaraterIndefinidoComPesoENotaMinima_RecusaSoOCarater(CaraterEtapa carater)
    {
        List<FieldError> erros = FormaBasica(carater, peso: 2m, notaMinima: 5m);

        Pares(erros).Should().Equal(("carater", "EtapaProcesso.CaraterObrigatorio"));
    }

    // ── Escala: nota mínima, nota máxima e passo ──

    [Fact(DisplayName = "Criar aceita a nota mínima igual à nota máxima")]
    public void Criar_NotaMinimaIgualANotaMaxima_Aceita()
    {
        Result<EtapaProcesso> resultado = Criar(new() { NotaMinima = 10m, NotaMaxima = 10m });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.NotaMinima.Should().Be(resultado.Value.NotaMaxima);
    }

    [Fact(DisplayName = "Criar recusa a nota mínima maior que a nota máxima no campo da nota mínima")]
    public void Criar_NotaMinimaMaiorQueANotaMaxima_Recusa()
    {
        Result<EtapaProcesso> resultado = Criar(new() { NotaMinima = 10.01m, NotaMaxima = 10m });

        Pares(resultado.Errors).Should().Equal(("notaMinima", EtapaProcessoErrorCodes.NotaMinimaAcimaDaNotaMaxima));
    }

    [Fact(DisplayName = "Nota mínima acima da máxima e máxima fora do passo acumulam, a mínima primeiro")]
    public void ValidarFormaBasica_MinimaAcimaEMaximaForaDoPasso_AcumulamNaOrdem()
    {
        List<FieldError> erros = FormaBasica(CaraterEtapa.Eliminatoria, notaMinima: 12m, notaMaxima: 10m, passo: 0.3m);

        Pares(erros).Should().Equal(
            ("notaMinima", EtapaProcessoErrorCodes.NotaMinimaAcimaDaNotaMaxima),
            ("notaMaxima", EtapaProcessoErrorCodes.NotaMaximaForaDoPasso));
    }

    [Fact(DisplayName = "Nota máxima inválida não é comparada com a nota mínima nem com o passo")]
    public void ValidarFormaBasica_NotaMaximaInvalida_NaoEComparada()
    {
        List<FieldError> erros = FormaBasica(CaraterEtapa.Eliminatoria, notaMinima: 5m, notaMaxima: 0m, passo: 0.3m);

        Pares(erros).Should().Equal(("notaMaxima", EtapaProcessoErrorCodes.NotaMaximaInvalida));
    }

    [Theory(DisplayName = "A nota máxima com casas decimais precisa ser múltipla do passo")]
    [InlineData(7.5, 0.5, true)]
    [InlineData(7.5, 0.4, false)]
    [InlineData(0.3, 0.1, true)]
    [InlineData(10, 10, true)]
    [InlineData(10, 20, false)]
    public void ValidarFormaBasica_NotaMaximaDecimalContraPasso_RecusaForaDoMultiplo(double notaMaxima, double passo, bool aceita)
    {
        List<FieldError> erros = FormaBasica(
            CaraterEtapa.Ambas, notaMaxima: (decimal)notaMaxima, passo: (decimal)passo);

        erros.Count.Should().Be(aceita ? 0 : 1);
    }

    // ── Escala de atendimento com elemento nulo ──

    private static List<FieldError> FormaBasicaComEscala(IReadOnlyList<NivelDeAtendimento?> escala) =>
        ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            "Entrevista", CaraterEtapa.Ambas, ordem: 1,
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: escala),
            itens: null);

    [Fact(DisplayName = "A escala com um nível completo e um elemento nulo é recusada na posição do nulo")]
    public void ValidarFormaBasica_EscalaComNivelCompletoEElementoNulo_RecusaNaPosicao()
    {
        List<FieldError> erros = FormaBasicaComEscala([new NivelDeAtendimento("Atendeu", 1m), null]);

        Pares(erros).Should().Equal(("escalaDeAtendimento[1]", NiveisDeAtendimento.RotuloInvalido));
    }

    [Fact(DisplayName = "A escala só com elemento nulo é recusada na posição e por não ter nível completo")]
    public void ValidarFormaBasica_EscalaSoComElementoNulo_RecusaNaPosicaoEPorFaltaDeNivelCompleto()
    {
        List<FieldError> erros = FormaBasicaComEscala([null]);

        Pares(erros).Should().Equal(
            ("escalaDeAtendimento[0]", NiveisDeAtendimento.RotuloInvalido),
            ("escalaDeAtendimento", NiveisDeAtendimento.SemNivelCompleto));
    }

    [Fact(DisplayName = "O elemento nulo ocupa índice: a recusa do nível seguinte cita a posição real")]
    public void ValidarFormaBasica_NivelInvalidoDepoisDeElementoNulo_CitaAPosicaoReal()
    {
        List<FieldError> erros = FormaBasicaComEscala([null, new NivelDeAtendimento("Excedeu", 1.5m)]);

        Pares(erros).Should().Equal(
            ("escalaDeAtendimento[0]", NiveisDeAtendimento.RotuloInvalido),
            ("escalaDeAtendimento[1].fracao", NiveisDeAtendimento.FracaoInvalida),
            ("escalaDeAtendimento", NiveisDeAtendimento.SemNivelCompleto));
    }

    [Fact(DisplayName = "Criar com a escala só de elemento nulo e itens recusa o nulo e a falta de nível completo")]
    public void Criar_EscalaSoComElementoNuloComItens_RecusaONuloEAFaltaDeNivelCompleto()
    {
        Result<EtapaProcesso> resultado = Criar(new()
        {
            Forma = FormaDeAvaliacao.Humana,
            Itens = [Item("Clareza", 10m)],
            Escala = [null!],
        });

        Pares(resultado.Errors).Should().Equal(
            ("escalaDeAtendimento[0]", NiveisDeAtendimento.RotuloInvalido),
            ("escalaDeAtendimento", NiveisDeAtendimento.SemNivelCompleto));
    }

    [Fact(DisplayName = "Criar com a escala só de elemento nulo e sem itens recusa o nulo, a falta de nível e a falta de itens, nesta ordem")]
    public void Criar_EscalaSoComElementoNuloSemItens_RecusaONuloAFaltaDeNivelCompletoEDeItens()
    {
        Result<EtapaProcesso> resultado = Criar(new() { Forma = FormaDeAvaliacao.Humana, Escala = [null!] });

        Pares(resultado.Errors).Should().Equal(
            ("escalaDeAtendimento[0]", NiveisDeAtendimento.RotuloInvalido),
            ("escalaDeAtendimento", NiveisDeAtendimento.SemNivelCompleto),
            ("escalaDeAtendimento", EtapaProcessoErrorCodes.EscalaSemItens));
    }

    [Fact(DisplayName = "Criar com um nível completo e um elemento nulo recusa o nulo em vez de lançar ao ordenar")]
    public void Criar_EscalaComNivelCompletoEElementoNulo_RecusaONulo()
    {
        Result<EtapaProcesso> resultado = Criar(new()
        {
            Forma = FormaDeAvaliacao.Humana,
            Itens = [Item("Clareza", 10m)],
            Escala = [new("Atendeu", 1m), null!],
        });

        Pares(resultado.Errors).Should().Equal(("escalaDeAtendimento[1]", NiveisDeAtendimento.RotuloInvalido));
    }
}
