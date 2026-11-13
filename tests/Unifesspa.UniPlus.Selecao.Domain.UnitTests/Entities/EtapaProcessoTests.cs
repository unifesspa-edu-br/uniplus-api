namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed partial class EtapaProcessoTests
{
    // ── A identidade do que não mudou sobrevive à regravação ──
    //
    // O hash canônico responde "esta configuração é a mesma que foi publicada?". Se cada
    // gravação sorteia ids novos para produtos e bancas que ninguém tocou, ele responde "não"
    // a um reenvio idêntico, e deixa de servir para o que existe.

    [Fact(DisplayName = "Regravar o mesmo par ato e papel preserva o Id do produto")]
    public void DefinirProdutos_MesmoAtoEPapel_PreservaOId()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 1m).Value!;

        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)])
            .IsSuccess.Should().BeTrue();
        Guid idOriginal = etapa.Produtos.Single().Id;

        // O cliente devolve a coleção que leu: instâncias novas, mesmo conteúdo.
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)])
            .IsSuccess.Should().BeTrue();

        etapa.Produtos.Single().Id.Should().Be(idOriginal,
            "o par ato e papel não mudou, e o id do produto entra nos bytes canônicos — sorteá-lo "
            + "de novo faz o hash da publicação mudar sem que nada da configuração tenha mudado");
    }

    [Fact(DisplayName = "Regravar a mesma banca preserva o Id")]
    public void DefinirBancas_MesmoCodigo_PreservaOId()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 1m).Value!;

        Guid tipoBanca = Guid.CreateVersion7();
        etapa.DefinirBancas([BancaDaEtapa.Criar(tipoBanca, "BANCA_TECNICA")]).IsSuccess.Should().BeTrue();
        Guid idOriginal = etapa.Bancas.Single().Id;

        etapa.DefinirBancas([BancaDaEtapa.Criar(tipoBanca, "BANCA_TECNICA")]).IsSuccess.Should().BeTrue();

        etapa.Bancas.Single().Id.Should().Be(idOriginal,
            "o código não mudou; recriar a linha gira o id e o CreatedAt sem que nada tenha mudado");
    }

    [Fact(DisplayName = "O mesmo tipo de ato com o mesmo papel duas vezes na etapa é recusado")]
    public void DefinirProdutos_MesmoTipoEMesmoPapel_Recusa()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 1m).Value!;

        Result resultado = etapa.DefinirProdutos([
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar),
        ]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("EtapaProcesso.AtoDuplicadoNaEtapa");
    }

    [Fact(DisplayName = "O mesmo tipo de ato uma vez como preliminar e uma como definitivo na etapa é aceito")]
    public void DefinirProdutos_MesmoTipoComCadaPapel_Aceita()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 1m).Value!;

        etapa.DefinirProdutos([
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Produto que sai da declaração some, e o que entra ganha Id próprio")]
    public void DefinirProdutos_ParQueMuda_TrocaDeLinha()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 1m).Value!;

        etapa.DefinirProdutos([
            ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        Guid idPreliminar = etapa.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id;

        // O definitivo sai, o preliminar fica, e um ato novo entra.
        etapa.DefinirProdutos([
            ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar),
        ]).IsSuccess.Should().BeTrue();

        etapa.Produtos.Should().HaveCount(2);
        etapa.Produtos.Single(p => p.AtoCodigo == "RESULTADO_CLASSIFICACAO").Id.Should().Be(idPreliminar,
            "preservar identidade não pode virar preservar conteúdo: o que continua declarado mantém a linha");
        etapa.Produtos.Should().ContainSingle(p => p.AtoCodigo == "GABARITO");
    }

    private static TipoEtapaSnapshot TipoEtapaProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    // ── Como a nota da etapa é obtida ──

    private static ReferenciaRegra RegraDeConsolidacao(string codigo = RegraConsolidacaoCodigo.AvaliacaoUnica) =>
        ReferenciaRegra.Criar(codigo, "v1", new string('c', 64)).Value!;

    private static TipoEtapaSnapshot TipoEtapaDoEnem() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!;

    [Theory(DisplayName = "A regra de consolidação só vale na forma humana: na objetiva, e sem forma, é recusada")]
    [InlineData(null)]
    [InlineData(FormaDeAvaliacao.Objetiva)]
    public void Criar_RegraDeConsolidacaoForaDaFormaHumana_Recusa(FormaDeAvaliacao? forma)
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Redação", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m,
            formaDeAvaliacao: forma, regraConsolidacao: RegraDeConsolidacao());

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana);
        resultado.Errors[0].Field.Should().Be("regraConsolidacao");
    }

    [Fact(DisplayName = "O passo do lançamento é recusado na forma objetiva, onde ninguém lança a nota")]
    public void Criar_PassoNaFormaObjetiva_Recusa()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m,
            notaMaxima: 10m, passoDoLancamento: 0.5m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EtapaProcesso.PassoNaFormaObjetiva);
        resultado.Errors[0].Field.Should().Be("passoDoLancamento");
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa forma e regra, cada uma no seu campo")]
    public void Criar_FormaERegraNaEtapaDoEnem_Recusa()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaDoEnem(), peso: 1m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana, regraConsolidacao: RegraDeConsolidacao());

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("formaDeAvaliacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem),
            ("regraConsolidacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem),
        ]);
    }

    [Fact(DisplayName = "A forma humana só aceita regra que seja do rol de consolidação")]
    public void Criar_RegraForaDoRolDeConsolidacao_Recusa()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Redação", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana, regraConsolidacao: RegraDeConsolidacao("PRECISAO-TRUNCAR"));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EtapaProcesso.RegraDeConsolidacaoInvalida);
    }

    [Fact(DisplayName = "Em rascunho, a forma humana sem regra e sem passo é gravada: a falta só recusa a publicação")]
    public void Criar_FormaHumanaSemRegraESemPasso_Grava()
    {
        EtapaProcesso.Criar(
            "Redação", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana).IsSuccess.Should().BeTrue();
        EtapaProcesso.Criar(
            "Redação", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m,
            notaMaxima: 10m, passoDoLancamento: 0.5m, formaDeAvaliacao: FormaDeAvaliacao.Humana,
            regraConsolidacao: RegraDeConsolidacao(RegraConsolidacaoCodigo.CorrecaoIndependente)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com dados válidos tem sucesso")]
    public void Criar_DadosValidos_Sucesso()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 3m, notaMinima: 5m, ordem: 1);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Nome.Should().Be("Prova Objetiva");
    }

    [Fact(DisplayName = "Criar sem peso/notaMinima/ordem (todos opcionais) tem sucesso")]
    public void Criar_SemCamposOpcionais_Sucesso()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Análise de histórico", CaraterEtapa.Eliminatoria, TipoEtapaProvaObjetiva());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com nome vazio falha com NomeObrigatorio")]
    public void Criar_NomeVazio_Falha()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "   ", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.NomeObrigatorio");
    }

    [Fact(DisplayName = "Criar com nome acima do limite falha com NomeTamanho")]
    public void Criar_NomeAcimaDoLimite_Falha()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            new string('a', EtapaProcesso.NomeMaxLength + 1), CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.NomeTamanho");
    }

    [Fact(DisplayName = "Criar com caráter Nenhum falha com CaraterObrigatorio")]
    public void Criar_CaraterNenhum_Falha()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Nenhum, TipoEtapaProvaObjetiva());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.CaraterObrigatorio");
    }

    [Fact(DisplayName = "Criar com valor de caráter fora do enum falha com CaraterObrigatorio")]
    public void Criar_CaraterForaDoEnum_Falha()
    {
        // O JsonStringEnumConverter global aceita inteiro cru (allowIntegerValues: true por
        // padrão) — sem esta checagem, "carater": 4 bindaria como (CaraterEtapa)4 e passaria
        // pela checagem de Nenhum, sendo persistido como valor indefinido.
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Prova Objetiva", (CaraterEtapa)4, TipoEtapaProvaObjetiva());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.CaraterObrigatorio");
    }

    [Fact(DisplayName = "Criar com peso não positivo falha com PesoInvalido")]
    public void Criar_PesoInvalido_Falha()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 0m);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.PesoInvalido");
    }

    [Fact(DisplayName = "Criar com nota mínima negativa falha com NotaMinimaInvalida")]
    public void Criar_NotaMinimaInvalida_Falha()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), notaMinima: -1m);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.NotaMinimaInvalida");
    }

    [Theory(DisplayName = "Nota máxima e passo não positivos são recusados, cada um no seu campo")]
    [InlineData(0.0, null, "EtapaProcesso.NotaMaximaInvalida", "notaMaxima")]
    [InlineData(-1.0, null, "EtapaProcesso.NotaMaximaInvalida", "notaMaxima")]
    [InlineData(null, 0.0, "EtapaProcesso.PassoDoLancamentoInvalido", "passoDoLancamento")]
    [InlineData(null, -0.5, "EtapaProcesso.PassoDoLancamentoInvalido", "passoDoLancamento")]
    public void ValidarFormaBasica_EscalaNaoPositiva_Recusa(double? notaMaxima, double? passo, string codigo, string campo)
    {
        List<FieldError> erros = EtapaProcesso.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, notaMinima: null, ordem: 1,
            notaMaxima: (decimal?)notaMaxima, passoDoLancamento: (decimal?)passo);

        erros.Should().ContainSingle().Which.Error.Code.Should().Be(codigo);
        erros[0].Field.Should().Be(campo);
    }

    [Fact(DisplayName = "Nota mínima acima da nota máxima é recusada, e igual é aceita")]
    public void ValidarFormaBasica_NotaMinimaContraNotaMaxima_RecusaSoAcima()
    {
        List<FieldError> acima = EtapaProcesso.ValidarFormaBasica(
            "Entrevista", CaraterEtapa.Eliminatoria, peso: null, notaMinima: 10.5m, ordem: 1, notaMaxima: 10m);
        List<FieldError> igual = EtapaProcesso.ValidarFormaBasica(
            "Entrevista", CaraterEtapa.Eliminatoria, peso: null, notaMinima: 10m, ordem: 1, notaMaxima: 10m);

        acima.Should().ContainSingle().Which.Error.Code.Should().Be("EtapaProcesso.NotaMinimaAcimaDaNotaMaxima");
        acima[0].Field.Should().Be("notaMinima");
        igual.Should().BeEmpty();
    }

    [Theory(DisplayName = "A nota máxima precisa ser múltipla do passo")]
    [InlineData(10, 0.25, true)]
    [InlineData(10, 0.5, true)]
    [InlineData(10, 1, true)]
    [InlineData(10, 0.01, true)]
    [InlineData(10, 0.3, false)]
    [InlineData(10, 4, false)]
    public void ValidarFormaBasica_NotaMaximaContraPasso_RecusaForaDoMultiplo(double notaMaxima, double passo, bool aceita)
    {
        List<FieldError> erros = EtapaProcesso.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, notaMinima: null, ordem: 1,
            notaMaxima: (decimal)notaMaxima, passoDoLancamento: (decimal)passo);

        if (aceita)
        {
            erros.Should().BeEmpty();
            return;
        }

        erros.Should().ContainSingle().Which.Error.Code.Should().Be("EtapaProcesso.NotaMaximaForaDoPasso");
        erros[0].Field.Should().Be("notaMaxima");
    }

    [Fact(DisplayName = "Escala parcial é aceita na gravação: sem passo, ou sem nota máxima")]
    public void ValidarFormaBasica_EscalaParcial_Aceita()
    {
        EtapaProcesso.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, notaMinima: null, ordem: 1, notaMaxima: 10m)
            .Should().BeEmpty();
        EtapaProcesso.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, notaMinima: null, ordem: 1, passoDoLancamento: 0.5m)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A nota mínima não precisa ser múltipla do passo")]
    public void ValidarFormaBasica_NotaMinimaForaDoPasso_Aceita()
    {
        EtapaProcesso.ValidarFormaBasica(
            "Entrevista", CaraterEtapa.Eliminatoria, peso: null, notaMinima: 5.1m, ordem: 1,
            notaMaxima: 10m, passoDoLancamento: 0.5m)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "Nota mínima já recusada por outro motivo não é comparada de novo com a nota máxima")]
    public void ValidarFormaBasica_NotaMinimaJaRecusada_NaoRepeteAComparacao()
    {
        List<FieldError> erros = EtapaProcesso.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, notaMinima: 12m, ordem: 1, notaMaxima: 10m);

        erros.Should().ContainSingle().Which.Error.Code.Should().Be("EtapaProcesso.NotaMinimaSemCaraterQueElimina");
    }
}
