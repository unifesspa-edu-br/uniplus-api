namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O validator confere só a forma da regra de recurso: o que cabe na coluna. Magnitude, unidade, efeito suspensivo, guarda da
/// vaga e prazo da 2ª instância são recusas do domínio, que acumula todas (ADR-0125).
/// </summary>
public sealed class DefinirCronogramaFasesCommandValidatorTests
{
    private static RegraRecursoFaseInput RegraRecurso(
        decimal? prazoValor = 48m,
        decimal? prazoSegundaInstanciaValor = null) => new(
            RegraCodigo: RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(),
            RegraVersao: "v1",
            PrazoValor: prazoValor,
            PrazoUnidade: "HORAS",
            AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
            AdmiteInstanciaSuperior: prazoSegundaInstanciaValor is not null,
            PrazoSegundaInstanciaValor: prazoSegundaInstanciaValor,
            PrazoSegundaInstanciaUnidade: prazoSegundaInstanciaValor is null ? null : "HORAS",
            EfeitoSuspensivo: "SEM_EFEITO_SUSPENSIVO",
            GuardaDaVaga: null);

    private static FaseCronogramaInput FaseValida(RegraRecursoFaseInput? regraRecurso = null) => new(
        Ordem: 1,
        FaseCanonicaId: Guid.CreateVersion7(),
        Inicio: null,
        Fim: null,
        Produtos: [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR")],
        EmiteParecerIndividual: false,
        BancasRequeridas: [],
        RegraRecurso: regraRecurso);

    private static DefinirCronogramaFasesCommand Comando(RegraRecursoFaseInput regraRecurso) => new(
        Guid.CreateVersion7(),
        [FaseValida(regraRecurso)]);

    [Fact(DisplayName = "Lista de fases vazia atravessa a validação de forma — quem recusa é o domínio")]
    public void Aceita_ListaDeFasesVazia_ParaODominioRecusar()
    {
        DefinirCronogramaFasesCommand comando = new(Guid.CreateVersion7(), []);

        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator().Validate(comando);

        resultado.Errors.Should().NotContain(
            e => e.PropertyName == nameof(DefinirCronogramaFasesCommand.Fases),
            "ProcessoSeletivo.CronogramaFasesVazio é a causa nomeada, e recusar aqui a tornava inalcançável");
    }

    /// <summary>
    /// As três coleções da fase são declaradas não-anuláveis, mas nada exige a chave no corpo:
    /// o host não habilita <c>RespectRequiredConstructorParameters</c>, então o parâmetro de
    /// construtor recebe nulo e o handler o percorre — falha de servidor onde deveria haver
    /// recusa nomeada. A lista vazia continua válida: é a declaração explícita de que a fase
    /// não publica, não requer banca, ou de que o tipo já identifica a banca sozinho.
    /// </summary>
    [Theory(DisplayName = "Validator recusa coleção nula na fase — o nulo não chega ao handler")]
    [InlineData("produtos", "Declare os produtos da fase")]
    [InlineData("bancas", "Declare as bancas requeridas pela fase")]
    [InlineData("categorias", "Declare as categorias de documento julgadas pela banca")]
    public void Rejeita_ColecaoNula(string colecao, string recusaEsperada)
    {
        FaseCronogramaInput fase = FaseComColecaoNula(colecao);

        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(new DefinirCronogramaFasesCommand(Guid.CreateVersion7(), [fase]));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.StartsWith(recusaEsperada, StringComparison.Ordinal));
    }

    private static FaseCronogramaInput FaseComColecaoNula(string colecao) => colecao switch
    {
        "produtos" => FaseValida() with { Produtos = null! },
        "bancas" => FaseValida() with { BancasRequeridas = null! },
        _ => FaseValida() with { BancasRequeridas = [new BancaRequeridaInput(Guid.CreateVersion7(), null!)] },
    };

    /// <summary>
    /// Item nulo dentro das coleções aninhadas passa incólume pelo <c>ChildRules</c>, e o
    /// handler o desreferencia — o tipo da banca, o papel do produto. Mesma proteção que o
    /// array de fases já tem um nível acima.
    /// </summary>
    [Theory(DisplayName = "Validator recusa item nulo nas coleções da fase")]
    [InlineData("produtos")]
    [InlineData("bancas")]
    public void Rejeita_ItemNuloNasColecoesAninhadas(string colecao)
    {
        FaseCronogramaInput fase = colecao == "produtos"
            ? FaseValida() with { Produtos = [null!] }
            : FaseValida() with { BancasRequeridas = [null!] };

        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(new DefinirCronogramaFasesCommand(Guid.CreateVersion7(), [fase]));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("não pode ser nulo", StringComparison.Ordinal));
    }

    [Theory(DisplayName = "Validator passa com prazo ausente, zero ou negativo: a recusa é do domínio, fonte única de validação (ADR-0125)")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Aceita_PrazoNaoPositivoNoValidator(int? valor)
    {
        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator()
            .Validate(Comando(RegraRecurso(prazoValor: valor)));

        resultado.IsValid.Should().BeTrue(
            "magnitude e unidade declarável são invariantes do agregado; repeti-las aqui devolveria a resposta "
            + "genérica do validator no lugar do erro de negócio nomeado");
    }

    [Fact(DisplayName = "Validator passa com o fim da fase antes do início — a rejeição é do agregado (FaseCronograma.Criar, ADR-0125), para poder acumular com outras violações da mesma fase")]
    public void Aceita_FimAntesDoInicioNoValidator()
    {
        FaseCronogramaInput fase = FaseValida() with
        {
            Inicio = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Produtos = [],
        };
        DefinirCronogramaFasesCommand command = new(Guid.CreateVersion7(), [fase]);

        ValidationResult resultado = new DefinirCronogramaFasesCommandValidator().Validate(command);

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }
}
