namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Invariantes de domínio do cadastro de tipos de ato: formato do código,
/// coerência da janela semiaberta de vigência e o predicado de vigência.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class TipoAtoPublicadoTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    [Fact(DisplayName = "Criar persiste os campos normalizados")]
    public void Criar_ComCamposValidos_NormalizaEPreenche()
    {
        Result<TipoAtoPublicado> resultado = TipoAtoPublicado.Criar(
            codigo: "  EDITAL_ABERTURA  ",
            nome: "  Edital de abertura  ",
            congelaConfiguracao: true,
            unicoPorObjeto: true,
            efeitoIrreversivel: false,
            vigenciaInicio: Inicio,
            vigenciaFim: null,
            baseLegal: "   ");

        resultado.IsSuccess.Should().BeTrue();
        TipoAtoPublicado tipo = resultado.Value!;
        tipo.Codigo.Should().Be("EDITAL_ABERTURA");
        tipo.Nome.Should().Be("Edital de abertura");
        tipo.CongelaConfiguracao.Should().BeTrue();
        tipo.UnicoPorObjeto.Should().BeTrue();
        tipo.EfeitoIrreversivel.Should().BeFalse();
        tipo.VigenciaFim.Should().BeNull();
        tipo.BaseLegal.Should().BeNull();
    }

    [Theory(DisplayName = "Código fora do formato UPPER_SNAKE é recusado")]
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

    [Fact(DisplayName = "Atualizar revalida os campos e não muta a entidade quando falha")]
    public void Atualizar_ComCodigoInvalido_NaoMuta()
    {
        TipoAtoPublicado tipo = Novo().Value!;

        Result resultado = tipo.Atualizar(
            codigo: "invalido",
            nome: "Outro nome",
            congelaConfiguracao: true,
            unicoPorObjeto: true,
            efeitoIrreversivel: true,
            vigenciaInicio: Inicio,
            vigenciaFim: null,
            baseLegal: null);

        resultado.IsFailure.Should().BeTrue();
        tipo.Codigo.Should().Be("EDITAL_ABERTURA");
        tipo.Nome.Should().Be("Edital de abertura");
    }

    [Fact(DisplayName = "Atualizar edita nome e atributos, mantendo o código")]
    public void Atualizar_ComMesmoCodigo_Sucesso()
    {
        TipoAtoPublicado tipo = Novo().Value!;

        Result resultado = tipo.Atualizar(
            codigo: "EDITAL_ABERTURA",
            nome: "Edital de abertura de processo seletivo",
            congelaConfiguracao: true,
            unicoPorObjeto: false,
            efeitoIrreversivel: false,
            vigenciaInicio: Inicio,
            vigenciaFim: null,
            baseLegal: null);

        resultado.IsSuccess.Should().BeTrue();
        tipo.Nome.Should().Be("Edital de abertura de processo seletivo");
        tipo.UnicoPorObjeto.Should().BeFalse();
    }

    [Fact(DisplayName = "Atualizar recusa novo código — o código é a identidade do tipo")]
    public void Atualizar_ComOutroCodigo_Recusa()
    {
        // A série de vigências agrupa-se pelo código (exclusion constraint), e a vaga que
        // um objeto reserva para uma linhagem de atos únicos é chaveada por ele
        // (ADR-0107). Renomear partiria o tipo em dois — e abriria uma segunda vaga no
        // mesmo objeto. Renomear é criar outro tipo.
        TipoAtoPublicado tipo = Novo().Value!;

        Result resultado = tipo.Atualizar(
            codigo: "EDITAL_RETIFICACAO",
            nome: "Edital de retificação",
            congelaConfiguracao: true,
            unicoPorObjeto: false,
            efeitoIrreversivel: false,
            vigenciaInicio: Inicio,
            vigenciaFim: null,
            baseLegal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoImutavel);
        tipo.Codigo.Should().Be("EDITAL_ABERTURA");
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

    private static Result<TipoAtoPublicado> Novo(
        string codigo = "EDITAL_ABERTURA",
        string nome = "Edital de abertura",
        DateOnly? vigenciaFim = null,
        string? baseLegal = null) =>
        TipoAtoPublicado.Criar(
            codigo,
            nome,
            congelaConfiguracao: true,
            unicoPorObjeto: true,
            efeitoIrreversivel: false,
            vigenciaInicio: Inicio,
            vigenciaFim: vigenciaFim,
            baseLegal: baseLegal);
}
