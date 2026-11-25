namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A validação dos campos da regra legal vive só no domínio e acumula toda violação do lote, cada uma
/// com o nome do campo no formato do contrato (ADR-0125).
/// </summary>
public sealed class ObrigatoriedadeLegalValidacaoTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly PredicadoObrigatoriedade Predicado = new EtapaObrigatoria("ProvaObjetiva");
    private static readonly TextoNormativo TextoValido = new("Descrição da regra.", "Lei 12.711/2012 art. 1º", null, null);

    private static Result Validar(
        string? tipo = "*",
        CategoriaObrigatoriedade? categoria = CategoriaObrigatoriedade.Etapa,
        string? regra = "ETAPA_OBRIGATORIA",
        PredicadoObrigatoriedade? predicado = null,
        TextoNormativo? texto = null,
        DateOnly? inicio = null,
        DateOnly? fim = null) =>
        ObrigatoriedadeLegal.ValidarCampos(tipo, categoria, regra, predicado ?? Predicado, texto ?? TextoValido, inicio ?? Inicio, fim);

    [Fact(DisplayName = "Campos válidos não geram violação")]
    public void ValidarCampos_Validos_Sucesso() => Validar().IsSuccess.Should().BeTrue();

    [Theory(DisplayName = "Cada regra do payload recusa com o código e o campo próprios")]
    [MemberData(nameof(CasosDeTexto))]
    [MemberData(nameof(CasosDeTipoCategoriaEVigencia))]
    public void ValidarCampos_Recusa_CodigoECampo(Func<Result> validar, string codigo, string campo)
    {
        ArgumentNullException.ThrowIfNull(validar);
        Result resultado = validar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Error.Code == codigo && e.Field == campo);
    }

    public static TheoryData<Func<Result>, string, string> CasosDeTexto() => new()
    {
        { () => Validar(regra: null), "ObrigatoriedadeLegal.RegraCodigoObrigatorio", "regraCodigo" },
        { () => Validar(regra: new string('a', 129)), "ObrigatoriedadeLegal.RegraCodigoInvalido", "regraCodigo" },
        { () => Validar(texto: TextoValido with { DescricaoHumana = null }), "ObrigatoriedadeLegal.DescricaoHumanaObrigatoria", "descricaoHumana" },
        { () => Validar(texto: TextoValido with { DescricaoHumana = new string('a', 1001) }), "ObrigatoriedadeLegal.DescricaoHumanaInvalida", "descricaoHumana" },
        { () => Validar(texto: TextoValido with { BaseLegal = " " }), "ObrigatoriedadeLegal.BaseLegalObrigatoria", "baseLegal" },
        { () => Validar(texto: TextoValido with { BaseLegal = new string('a', 501) }), "ObrigatoriedadeLegal.BaseLegalInvalida", "baseLegal" },
        { () => Validar(texto: TextoValido with { AtoNormativo = new string('a', 1001) }), "ObrigatoriedadeLegal.AtoNormativoUrlInvalido", "atoNormativoUrl" },
        { () => Validar(texto: TextoValido with { PortariaInternaCodigo = new string('a', 129) }), "ObrigatoriedadeLegal.PortariaInternaCodigoInvalido", "portariaInternaCodigo" },
    };

    public static TheoryData<Func<Result>, string, string> CasosDeTipoCategoriaEVigencia() => new()
    {
        { () => Validar(tipo: null), "ObrigatoriedadeLegal.TipoProcessoCodigoObrigatorio", "tipoProcessoCodigo" },
        { () => Validar(tipo: "  "), "ObrigatoriedadeLegal.TipoProcessoCodigoObrigatorio", "tipoProcessoCodigo" },
        { () => Validar(tipo: new string('a', 65)), "ObrigatoriedadeLegal.TipoProcessoCodigoInvalido", "tipoProcessoCodigo" },
        { () => Validar(categoria: null), "ObrigatoriedadeLegal.CategoriaInvalida", "categoria" },
        { () => Validar(categoria: (CategoriaObrigatoriedade)999), "ObrigatoriedadeLegal.CategoriaInvalida", "categoria" },
        { () => Validar(fim: Inicio), "ObrigatoriedadeLegal.VigenciaInvalida", "vigenciaFim" },
        { () => Validar(fim: Inicio.AddDays(-1)), "ObrigatoriedadeLegal.VigenciaInvalida", "vigenciaFim" },
        { () => SemInicio(), "ObrigatoriedadeLegal.VigenciaInicioObrigatorio", "vigenciaInicio" },
        { () => SemPredicado(), "ObrigatoriedadeLegal.PredicadoObrigatorio", "predicado" },
        { () => Validar(predicado: new EtapaObrigatoria(" ")), "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco", "predicado" },
        { () => Validar(predicado: new ModalidadesMinimas([])), "ObrigatoriedadeLegal.ModalidadesMinimasVazia", "predicado" },
    };

    private static Result SemInicio() =>
        ObrigatoriedadeLegal.ValidarCampos("*", CategoriaObrigatoriedade.Etapa, "X", Predicado, TextoValido, null, null);

    private static Result SemPredicado() =>
        ObrigatoriedadeLegal.ValidarCampos("*", CategoriaObrigatoriedade.Etapa, "X", null, TextoValido, Inicio, null);

    private static readonly string[] CamposNaOrdem =
        ["tipoProcessoCodigo", "categoria", "regraCodigo", "predicado", "descricaoHumana", "baseLegal", "vigenciaInicio"];

    [Fact(DisplayName = "Todas as violações do lote voltam juntas, na ordem dos campos")]
    public void ValidarCampos_VariosErros_Acumula()
    {
        Result resultado = ObrigatoriedadeLegal.ValidarCampos(
            null, null, "", null, new TextoNormativo(null, "", null, null), null, null);

        resultado.Errors.Select(e => e.Field).Should().Equal(CamposNaOrdem);
    }

    [Fact(DisplayName = "Texto invalido e codigo invalido no mesmo payload saem os dois")]
    public void ValidarCampos_TextoECodigoInvalidos_SaemOsDois()
    {
        Result resultado = Validar(regra: "", texto: TextoValido with { DescricaoHumana = "" });

        resultado.Errors.Select(e => e.Field).Should().Equal("regraCodigo", "descricaoHumana");
    }

    [Fact(DisplayName = "Criar com campos inválidos devolve a falha de validação com todos os erros")]
    public void Criar_Invalido_FalhaDeValidacaoAcumulada()
    {
        Result<ObrigatoriedadeLegal> resultado = ObrigatoriedadeLegal.Criar(
            "*", null, "", Predicado, TextoValido, Inicio, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().Equal("categoria", "regraCodigo");
    }

    [Fact(DisplayName = "Criar aplica trim e calcula o hash do conteúdo final")]
    public void Criar_Valido_NormalizaEComputaHash()
    {
        Result<ObrigatoriedadeLegal> resultado = ObrigatoriedadeLegal.Criar(
            " * ", CategoriaObrigatoriedade.Etapa, "REGRA", Predicado,
            new TextoNormativo("  desc  ", "  lei  ", "  ", " portaria "), Inicio, null);

        ObrigatoriedadeLegal regra = resultado.Value!;
        regra.TipoProcessoCodigo.Should().Be("*");
        regra.RegraCodigo.Should().Be("REGRA");
        regra.DescricaoHumana.Should().Be("desc");
        regra.BaseLegal.Should().Be("lei");
        regra.AtoNormativoUrl.Should().BeNull();
        regra.PortariaInternaCodigo.Should().Be("portaria");
        HashCanonicalComputer.IsValidHashShape(regra.Hash).Should().BeTrue();
    }

    [Theory]
    [InlineData(" X ")]
    [InlineData("A__B")]
    [InlineData("_A")]
    [InlineData("A_")]
    [InlineData("1A")]
    [InlineData("A\n")]
    public void Criar_RegraCodigoForaDoFormato_RecusaSemRepetirValor(string codigo)
    {
        Result resultado = Validar(regra: codigo);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ObrigatoriedadeLegal.RegraCodigoInvalido");
        resultado.Error!.Message.Should().NotContain(codigo);
    }

    [Fact]
    public void Criar_RegraCodigoNosLimites_Aceita()
    {
        Validar(regra: "A").IsSuccess.Should().BeTrue();
        Validar(regra: new string('A', 80)).IsSuccess.Should().BeTrue();
        Validar(regra: new string('A', 81)).Error!.Code.Should().Be("ObrigatoriedadeLegal.RegraCodigoInvalido");
    }

    [Fact(DisplayName = "Atualizar inválido não muda o estado da regra")]
    public void Atualizar_Invalido_NaoMutaOAgregado()
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            "*", CategoriaObrigatoriedade.Etapa, "REGRA", Predicado, TextoValido, Inicio, null).Value!;
        string hashAntes = regra.Hash;

        Result resultado = regra.Atualizar("*", CategoriaObrigatoriedade.Etapa, "", Predicado, TextoValido, Inicio, null);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("regraCodigo");
        regra.RegraCodigo.Should().Be("REGRA");
        regra.Hash.Should().Be(hashAntes);
    }

    [Fact(DisplayName = "Atualizar válido aplica o texto e recomputa o hash")]
    public void Atualizar_Valido_AplicaERecomputa()
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            "*", CategoriaObrigatoriedade.Etapa, "REGRA", Predicado, TextoValido, Inicio, null).Value!;
        string hashAntes = regra.Hash;

        Result resultado = regra.Atualizar("*", CategoriaObrigatoriedade.Etapa, "OUTRA", Predicado, TextoValido, Inicio, null);

        resultado.IsSuccess.Should().BeTrue();
        regra.RegraCodigo.Should().Be("OUTRA");
        regra.Hash.Should().NotBe(hashAntes);
    }

    [Fact(DisplayName = "A forma do predicado devolve a violação já com o campo")]
    public void ValidarFormaDoPredicado_Falha_TrazOCampo()
    {
        Result forma = ObrigatoriedadeLegal.ValidarFormaDoPredicado(new EtapaObrigatoria(" "));

        forma.Errors.Should().ContainSingle().Which.Field.Should().Be("predicado");
    }
}
