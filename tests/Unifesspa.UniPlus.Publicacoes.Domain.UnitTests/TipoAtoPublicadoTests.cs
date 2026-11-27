namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Invariantes de domínio do cadastro de tipos de ato: formato do código,
/// coerência da janela semiaberta de vigência e o predicado de vigência.
/// </summary>
public sealed class TipoAtoPublicadoTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    [Fact(DisplayName = "Criar persiste os campos normalizados")]
    public void Criar_ComCamposValidos_NormalizaEPreenche()
    {
        Result<TipoAtoPublicado> resultado = CriarComCamposNormalizados();

        resultado.IsSuccess.Should().BeTrue();
        TipoAtoPublicado tipo = resultado.Value!;
        tipo.Codigo.Should().Be("EDITAL_ABERTURA");
        tipo.Nome.Should().Be("Edital de abertura");
        tipo.CongelaConfiguracao.Should().BeTrue();
        tipo.UnicoPorObjeto.Should().BeTrue();
        tipo.EfeitoIrreversivel.Should().BeFalse();
        tipo.EhResultado.Should().BeTrue();
        tipo.VigenciaFim.Should().BeNull();
        tipo.BaseLegal.Should().BeNull();
    }

    [Fact(DisplayName = "Criar e Atualizar gravam os atributos do documento avulso; sem eles o tipo nasce não publicável")]
    public void CriarEAtualizar_GravamOsAtributosDoDocumentoAvulso()
    {
        TipoAtoPublicadoDeTeste aviso = new() { Codigo = "AVISO", Nome = "Aviso" };
        TipoAtoPublicado padrao = aviso.Criar().Value!;
        padrao.PublicavelComoAvulso.Should().BeFalse();
        padrao.ExigeAtoCorrigido.Should().BeFalse();

        TipoAtoPublicadoDeTeste declaracao = DeclaracaoDeErrata();
        TipoAtoPublicado errata = declaracao.Criar().Value!;
        errata.PublicavelComoAvulso.Should().BeTrue();
        errata.ExigeAtoCorrigido.Should().BeTrue();
        (declaracao with { ExigeAtoCorrigido = false }).Atualizar(errata).IsSuccess.Should().BeTrue();
        errata.ExigeAtoCorrigido.Should().BeFalse();
    }

    private static TipoAtoPublicadoDeTeste DeclaracaoDeErrata() =>
        new()
        {
            Codigo = "ERRATA",
            Nome = "Errata",
            PublicavelComoAvulso = true,
            ExigeAtoCorrigido = true
        };

    [Theory(DisplayName = "Código fora do formato UPPER_SNAKE é recusado")]
    [InlineData(" EDITAL_ABERTURA ")]
    [InlineData("edital_abertura")]
    [InlineData("5")]
    [InlineData("convocacao")]
    [InlineData("Convocacao")]
    [InlineData("_EDITAL")]
    [InlineData("EDITAL_")]
    [InlineData("EDITAL__ABERTURA")]
    [InlineData("EDITAL 2026")]
    [InlineData("EDITAL-ABERTURA")]
    [InlineData("RETIFICAÇÃO")]
    public void Criar_ComCodigoForaDoFormato_Falha(string codigo)
    {
        Result<TipoAtoPublicado> resultado = Novo(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoFormato);
    }

    [Fact(DisplayName = "Código em minúsculas é recusado, não convertido em silêncio")]
    public void Criar_ComCodigoMinusculo_NaoNormalizaCaixa()
    {
        // A coluna é `text` e o Postgres compara case-sensitive. Converter a caixa
        // esconderia do usuário que `convocacao` e `CONVOCACAO` seriam o mesmo tipo.
        Novo(codigo: "convocacao").IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Código em branco é recusado")]
    public void Criar_ComCodigoEmBranco_Falha()
    {
        Result<TipoAtoPublicado> resultado = Novo(codigo: "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "Nome com menos de dois caracteres é recusado")]
    public void Criar_ComNomeCurto_Falha()
    {
        Result<TipoAtoPublicado> resultado = Novo(nome: "E");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Janela vazia (fim igual ao início) é recusada — o fim é exclusivo")]
    public void Criar_ComFimIgualAoInicio_Falha()
    {
        Result<TipoAtoPublicado> resultado = Novo(vigenciaFim: Inicio);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaFimAnteriorAoInicio);
    }

    [Fact(DisplayName = "Janela invertida é recusada")]
    public void Criar_ComFimAnteriorAoInicio_Falha()
    {
        Result<TipoAtoPublicado> resultado = Novo(vigenciaFim: Inicio.AddDays(-1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaFimAnteriorAoInicio);
    }

    [Fact(DisplayName = "Janela de um único dia é aceita")]
    public void Criar_ComJanelaDeUmDia_Sucesso()
    {
        Novo(vigenciaFim: Inicio.AddDays(1)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Base legal acima do limite é recusada")]
    public void Criar_ComBaseLegalLonga_Falha()
    {
        Result<TipoAtoPublicado> resultado = Novo(baseLegal: new string('x', 501));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.BaseLegalTamanho);
    }

    [Fact(DisplayName = "Atualizar com o código do tipo acrescido de espaços é recusado, sem aparar")]
    public void Atualizar_ComCodigoComEspacos_RecusaSemAparar()
    {
        TipoAtoPublicado tipo = Novo(codigo: "EDITAL_ABERTURA").Value!;

        Result resultado = tipo.ConferirCodigoInalterado(" EDITAL_ABERTURA ");

        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoImutavel);
    }

    [Fact(DisplayName = "Atualizar revalida os campos e não muta a entidade quando falha")]
    public void Atualizar_ComCodigoInvalido_NaoMuta()
    {
        TipoAtoPublicado tipo = Novo().Value!;

        Result resultado = (new TipoAtoPublicadoDeTeste
        {
            Codigo = "invalido",
            Nome = "Outro nome",
            CongelaConfiguracao = true,
            UnicoPorObjeto = true,
            EfeitoIrreversivel = true,
            VigenciaInicio = Inicio
        }).Atualizar(tipo);

        ConferirRecusaSemAlterarTipo(resultado, tipo);
    }

    [Fact(DisplayName = "Atualizar edita nome e atributos, mantendo o código")]
    public void Atualizar_ComMesmoCodigo_Sucesso()
    {
        TipoAtoPublicado tipo = Novo().Value!;

        Result resultado = (new TipoAtoPublicadoDeTeste
        {
            Codigo = "EDITAL_ABERTURA",
            Nome = "Edital de abertura de processo seletivo",
            CongelaConfiguracao = true,
            EhResultado = true,
            VigenciaInicio = Inicio
        }).Atualizar(tipo);

        ConferirAtributosAtualizados(resultado, tipo);
    }

    [Fact(DisplayName = "Atualizar recusa novo código — o código é a identidade do tipo")]
    public void Atualizar_ComOutroCodigo_Recusa()
    {
        TipoAtoPublicado tipo = Novo().Value!;

        Result resultado = (new TipoAtoPublicadoDeTeste
        {
            Codigo = "EDITAL_RETIFICACAO",
            Nome = "Edital de retificação",
            CongelaConfiguracao = true,
            VigenciaInicio = Inicio
        }).Atualizar(tipo);

        ConferirCodigoImutavel(resultado, tipo);
    }

    [Theory(DisplayName = "A janela é semiaberta: o início entra, o fim não")]
    [InlineData(2025, 12, 31, false)]
    [InlineData(2026, 1, 1, true)]
    [InlineData(2026, 5, 31, true)]
    [InlineData(2026, 6, 1, false)]
    [InlineData(2026, 6, 2, false)]
    public void EstaVigenteEm_JanelaFechada(int ano, int mes, int dia, bool esperado)
    {
        TipoAtoPublicado tipo = Novo(vigenciaFim: new DateOnly(2026, 6, 1)).Value!;

        tipo.EstaVigenteEm(new DateOnly(ano, mes, dia)).Should().Be(esperado);
    }

    [Fact(DisplayName = "Vigência aberta não tem fim")]
    public void EstaVigenteEm_JanelaAberta()
    {
        TipoAtoPublicado tipo = Novo(vigenciaFim: null).Value!;

        tipo.EstaVigenteEm(Inicio.AddYears(50)).Should().BeTrue();
        tipo.EstaVigenteEm(Inicio.AddDays(-1)).Should().BeFalse();
    }

    [Fact(DisplayName = "Criar com três violações independentes acumula as três no mesmo lote")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<TipoAtoPublicado> resultado = CriarComTresViolacoes();

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors.Should().Contain(e => e.Field == "codigo" && e.Error.Code == TipoAtoPublicadoErrorCodes.CodigoFormato);
        resultado.Errors.Should().Contain(e => e.Field == "nome" && e.Error.Code == TipoAtoPublicadoErrorCodes.NomeTamanho);
        resultado.Errors.Should().Contain(e => e.Field == "vigenciaFim" && e.Error.Code == TipoAtoPublicadoErrorCodes.VigenciaFimAnteriorAoInicio);
    }

    [Fact(DisplayName = "Criar recusa o tipo que é resultado e de efeito irreversível, acumulando com as demais violações")]
    public void Criar_ResultadoComEfeitoIrreversivel_RecusaEAcumula()
    {
        Result<TipoAtoPublicado> resultado = CriarResultadoIrreversivel();

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "codigo" && e.Error.Code == TipoAtoPublicadoErrorCodes.CodigoFormato);
        resultado.Errors.Should().Contain(e => e.Field == "efeitoIrreversivel" && e.Error.Code == TipoAtoPublicadoErrorCodes.ResultadoComEfeitoIrreversivel);
    }

    [Fact(DisplayName = "Atualizar recusa tornar irreversível o tipo que é resultado e não muta a entidade")]
    public void Atualizar_ResultadoComEfeitoIrreversivel_RecusaENaoMuta()
    {
        TipoAtoPublicadoDeTeste declaracao = new() { Codigo = "RESULTADO_FINAL", Nome = "Resultado final", EhResultado = true };
        TipoAtoPublicado tipo = declaracao.Criar().Value!;
        Result resultado = (declaracao with { EfeitoIrreversivel = true }).Atualizar(tipo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == TipoAtoPublicadoErrorCodes.ResultadoComEfeitoIrreversivel);
        tipo.EfeitoIrreversivel.Should().BeFalse();
    }

    [Fact(DisplayName = "ValidarDeclaracao replica o resultado de Criar sem instanciar o agregado")]
    public void ValidarDeclaracao_MesmoPayloadDeCriarComFalha_RetornaMesmosErros()
    {
        Result<CamposDoTipoAtoPublicado> validacao = TipoAtoPublicado.ValidarDeclaracao((new TipoAtoPublicadoDeTeste
        {
            Codigo = null,
            Nome = null,
            VigenciaInicio = Inicio
        }).Declaracao());

        validacao.IsFailure.Should().BeTrue();
        validacao.Errors.Should().ContainSingle(e => e.Field == "codigo" && e.Error.Code == TipoAtoPublicadoErrorCodes.CodigoObrigatorio);
        validacao.Errors.Should().ContainSingle(e => e.Field == "nome" && e.Error.Code == TipoAtoPublicadoErrorCodes.NomeObrigatorio);
    }

    private static Result<TipoAtoPublicado> CriarComCamposNormalizados() =>
        (new TipoAtoPublicadoDeTeste
        {
            Codigo = "EDITAL_ABERTURA",
            Nome = "  Edital de abertura  ",
            CongelaConfiguracao = true,
            UnicoPorObjeto = true,
            EhResultado = true,
            VigenciaInicio = Inicio,
            BaseLegal = "   "
        }).Criar();

    private static Result<TipoAtoPublicado> CriarComTresViolacoes() =>
        (new TipoAtoPublicadoDeTeste
        {
            Codigo = "edital abertura",
            Nome = "E",
            CongelaConfiguracao = true,
            UnicoPorObjeto = true,
            VigenciaInicio = Inicio,
            VigenciaFim = Inicio.AddDays(-1)
        }).Criar();

    private static Result<TipoAtoPublicado> CriarResultadoIrreversivel() =>
        (new TipoAtoPublicadoDeTeste
        {
            Codigo = "resultado final",
            Nome = "Resultado final",
            EfeitoIrreversivel = true,
            EhResultado = true,
            VigenciaInicio = Inicio
        }).Criar();

    private static void ConferirRecusaSemAlterarTipo(Result resultado, TipoAtoPublicado tipo)
    {
        resultado.IsFailure.Should().BeTrue();
        tipo.Codigo.Should().Be("EDITAL_ABERTURA");
        tipo.Nome.Should().Be("Edital de abertura");
    }

    private static void ConferirAtributosAtualizados(Result resultado, TipoAtoPublicado tipo)
    {
        resultado.IsSuccess.Should().BeTrue();
        tipo.Nome.Should().Be("Edital de abertura de processo seletivo");
        tipo.UnicoPorObjeto.Should().BeFalse();
        tipo.EhResultado.Should().BeTrue();
    }

    private static void ConferirCodigoImutavel(Result resultado, TipoAtoPublicado tipo)
    {
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoImutavel);
        tipo.Codigo.Should().Be("EDITAL_ABERTURA");
    }

    private static Result<TipoAtoPublicado> Novo(
        string codigo = "EDITAL_ABERTURA",
        string nome = "Edital de abertura",
        DateOnly? vigenciaFim = null,
        string? baseLegal = null) =>
        (new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            Nome = nome,
            CongelaConfiguracao = true,
            UnicoPorObjeto = true,
            VigenciaInicio = Inicio,
            VigenciaFim = vigenciaFim,
            BaseLegal = baseLegal
        }).Criar();
}
