namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EtapaProcessoTests
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

        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)])
            .IsSuccess.Should().BeTrue();
        Guid idOriginal = etapa.Produtos.Single().Id;

        // O cliente devolve a coleção que leu: instâncias novas, mesmo conteúdo.
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)])
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

    [Fact(DisplayName = "Produto que sai da declaração some, e o que entra ganha Id próprio")]
    public void DefinirProdutos_ParQueMuda_TrocaDeLinha()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 1m).Value!;

        etapa.DefinirProdutos([
            ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        Guid idPreliminar = etapa.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id;

        // O definitivo sai, o preliminar fica, e um ato novo entra.
        etapa.DefinirProdutos([
            ProdutoDaEtapa.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar),
        ]).IsSuccess.Should().BeTrue();

        etapa.Produtos.Should().HaveCount(2);
        etapa.Produtos.Single(p => p.AtoCodigo == "RESULTADO_PRELIMINAR").Id.Should().Be(idPreliminar,
            "preservar identidade não pode virar preservar conteúdo: o que continua declarado mantém a linha");
        etapa.Produtos.Should().ContainSingle(p => p.AtoCodigo == "GABARITO");
    }

    private static TipoEtapaSnapshot TipoEtapaProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!;

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

    [Fact(DisplayName = "Criar com ordem não positiva falha com OrdemInvalida")]
    public void Criar_OrdemInvalida_Falha()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), ordem: 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.OrdemInvalida");
    }

    [Fact(DisplayName = "ADR-0125: todas as violações simultâneas acumulam no mesmo lote")]
    public void Criar_ViolacoesSimultaneas_AcumulaTodasNoMesmoLote()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(
            string.Empty, CaraterEtapa.Nenhum, TipoEtapaProvaObjetiva(), peso: 0m, notaMinima: -1m, ordem: 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "EtapaProcesso.NomeObrigatorio",
            "EtapaProcesso.CaraterObrigatorio",
            "EtapaProcesso.PesoInvalido",
            "EtapaProcesso.NotaMinimaInvalida",
            "EtapaProcesso.OrdemInvalida",
        ]);
    }

    /// <summary>
    /// Peso em etapa que não pontua fica gravado sem nunca entrar no divisor da média: quem
    /// configurou acredita que pesa, e o cálculo o ignora.
    /// </summary>
    [Fact(DisplayName = "Peso em etapa apenas eliminatória é recusado")]
    public void ValidarFormaBasica_PesoEmEtapaApenasEliminatoria_Recusa()
    {
        List<FieldError> erros = EtapaProcesso.ValidarFormaBasica(
            "Banca de heteroidentificação", CaraterEtapa.Eliminatoria, peso: 2m, notaMinima: null, ordem: 1);

        erros.Should().ContainSingle()
            .Which.Error.Code.Should().Be("EtapaProcesso.PesoSemCaraterQuePontua");
        erros[0].Field.Should().Be("peso");
    }

    [Fact(DisplayName = "Nota mínima em etapa apenas classificatória é recusada")]
    public void ValidarFormaBasica_NotaMinimaEmEtapaApenasClassificatoria_Recusa()
    {
        List<FieldError> erros = EtapaProcesso.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, notaMinima: 5m, ordem: 1);

        erros.Should().ContainSingle()
            .Which.Error.Code.Should().Be("EtapaProcesso.NotaMinimaSemCaraterQueElimina");
        erros[0].Field.Should().Be("notaMinima");
    }

    /// <summary>
    /// Caráter ausente já é reportado por si; um segundo erro dizendo que o peso não combina com
    /// ele mandaria a pessoa corrigir um campo que não está errado.
    /// </summary>
    [Fact(DisplayName = "Caráter ausente não gera também erro de coerência de peso")]
    public void ValidarFormaBasica_CaraterAusenteComPeso_ReportaSoOCarater()
    {
        List<FieldError> erros = EtapaProcesso.ValidarFormaBasica(
            "Etapa", CaraterEtapa.Nenhum, peso: 3m, notaMinima: 5m, ordem: 1);

        erros.Should().ContainSingle()
            .Which.Error.Code.Should().Be("EtapaProcesso.CaraterObrigatorio");
    }

    [Theory(DisplayName = "Caráter que o tipo de etapa não admite é recusado")]
    [InlineData(CaraterEtapa.Classificatoria, false, true, 1)]
    [InlineData(CaraterEtapa.Eliminatoria, true, false, 1)]
    [InlineData(CaraterEtapa.Ambas, false, false, 2)]
    [InlineData(CaraterEtapa.Ambas, true, true, 0)]
    [InlineData(CaraterEtapa.Eliminatoria, false, true, 0)]
    public void ValidarCaraterAdmitido_ConfereContraOQueOTipoAdmite(
        CaraterEtapa carater, bool admitePontuacao, bool admiteEliminacao, int violacoesEsperadas)
    {
        List<FieldError> erros = EtapaProcesso.ValidarCaraterAdmitido(
            carater, admitePontuacao, admiteEliminacao, "Análise Documental");

        erros.Should().HaveCount(violacoesEsperadas);
        erros.Should().AllSatisfy(erro =>
        {
            erro.Field.Should().Be("carater");
            erro.Error.Code.Should().Be(EtapaProcesso.CaraterNaoAdmitidoPeloTipo);
        });
    }

    /// <summary>
    /// A mensagem nomeia o tipo porque quem configura escolheu um tipo e um caráter: sem o nome,
    /// a recusa não diz qual dos dois revisar.
    /// </summary>
    [Fact(DisplayName = "A recusa de caráter nomeia o tipo de etapa que não o admite")]
    public void ValidarCaraterAdmitido_Recusa_NomeiaOTipo()
    {
        List<FieldError> erros = EtapaProcesso.ValidarCaraterAdmitido(
            CaraterEtapa.Classificatoria, admitePontuacao: false, admiteEliminacao: true, "Análise Documental");

        erros.Should().ContainSingle()
            .Which.Error.Message.Should().Contain("Análise Documental");
    }

    [Fact(DisplayName = "ValidarFormaBasica sem violação retorna lote vazio")]
    public void ValidarFormaBasica_SemViolacao_Vazio()
    {
        List<FieldError> erros = EtapaProcesso.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Ambas, peso: 3m, notaMinima: 5m, ordem: 1);

        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "AtualizarDados com dados válidos tem sucesso e substitui os campos")]
    public void AtualizarDados_DadosValidos_Sucesso()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;

        Result resultado = etapa.AtualizarDados(
            "Prova Objetiva (revisada)", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 2m, notaMinima: 5m, ordem: 2);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Nome.Should().Be("Prova Objetiva (revisada)");
        etapa.Carater.Should().Be(CaraterEtapa.Ambas);
        etapa.Peso.Should().Be(2m);
        etapa.NotaMinima.Should().Be(5m);
        etapa.Ordem.Should().Be(2);
    }

    [Fact(DisplayName = "AtualizarDados com nome vazio falha e não muda o estado anterior")]
    public void AtualizarDados_NomeVazio_FalhaSemMutarEstado()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;

        Result resultado = etapa.AtualizarDados(
            "   ", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, notaMinima: null, ordem: 1);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.NomeObrigatorio");
        etapa.Nome.Should().Be("Prova Objetiva");
    }
}
