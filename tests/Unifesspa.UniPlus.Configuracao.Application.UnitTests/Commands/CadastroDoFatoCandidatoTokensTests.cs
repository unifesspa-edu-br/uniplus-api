namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O token de cada eixo é interpretado do mesmo modo no cadastro do fato declarado, do derivado e do
/// agregado: ausente, vazio ou só espaços é "não informado" (obrigatório) e o preenchido que não é do
/// vocabulário é inválido.
/// </summary>
public sealed class CadastroDoFatoCandidatoTokensTests
{
    private const string Finalidade = "Enquadramento na reserva de vagas.";

    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IPrecedenciaFaseRepository _precedencias = Substitute.For<IPrecedenciaFaseRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    public CadastroDoFatoCandidatoTokensTests()
    {
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([]);
        _precedencias.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    public static TheoryData<string, string, string> EixosDeProtecao => new()
    {
        { "classificacaoProtecao", "", FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria },
        { "classificacaoProtecao", "   ", FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria },
        { "classificacaoProtecao", "SENSIVELL", FatoCandidatoErrorCodes.ClassificacaoProtecaoInvalida },
        { "hipoteseLegal", "", FatoCandidatoErrorCodes.HipoteseLegalObrigatoria },
        { "hipoteseLegal", " ", FatoCandidatoErrorCodes.HipoteseLegalObrigatoria },
        { "hipoteseLegal", "NAO_EXISTE", FatoCandidatoErrorCodes.HipoteseLegalInvalida },
    };

    public static TheoryData<string, string> Dominios => new()
    {
        { "", FatoCandidatoErrorCodes.DominioObrigatorio },
        { "  ", FatoCandidatoErrorCodes.DominioObrigatorio },
        { "BOOL", FatoCandidatoErrorCodes.DominioInvalido },
    };

    [Theory(DisplayName = "Declarado: proteção ausente é obrigatória e desconhecida é inválida")]
    [MemberData(nameof(EixosDeProtecao))]
    public async Task Declarado_Protecao(string campo, string token, string codigo)
    {
        Result<Guid> resultado = await CriarDeclaradoAsync(
            Declarado() with
            {
                ClassificacaoProtecao = campo == "classificacaoProtecao" ? token : "PESSOAL",
                HipoteseLegal = campo == "hipoteseLegal" ? token : "CUMPRIMENTO_OBRIGACAO_LEGAL",
            });

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e => e.Field == campo && e.Error.Code == codigo);
    }

    [Theory(DisplayName = "Derivado: proteção ausente é obrigatória e desconhecida é inválida")]
    [MemberData(nameof(EixosDeProtecao))]
    public async Task Derivado_Protecao(string campo, string token, string codigo)
    {
        Result<Guid> resultado = await CriarDerivadoAsync(
            Derivado() with
            {
                ClassificacaoProtecao = campo == "classificacaoProtecao" ? token : "PESSOAL",
                HipoteseLegal = campo == "hipoteseLegal" ? token : "CUMPRIMENTO_OBRIGACAO_LEGAL",
            });

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e => e.Field == campo && e.Error.Code == codigo);
    }

    [Theory(DisplayName = "Agregado: proteção ausente é obrigatória e desconhecida é inválida")]
    [MemberData(nameof(EixosDeProtecao))]
    public async Task Agregado_Protecao(string campo, string token, string codigo)
    {
        Result<Guid> resultado = await CriarAgregadoAsync(
            Agregado() with
            {
                ClassificacaoProtecao = campo == "classificacaoProtecao" ? token : "PESSOAL",
                HipoteseLegal = campo == "hipoteseLegal" ? token : "CUMPRIMENTO_OBRIGACAO_LEGAL",
            });

        resultado.Errors.Should().Contain(e => e.Field == campo && e.Error.Code == codigo);
    }

    [Theory(DisplayName = "Declarado: domínio vazio é obrigatório e desconhecido é inválido")]
    [MemberData(nameof(Dominios))]
    public async Task Declarado_Dominio(string token, string codigo)
    {
        Result<Guid> resultado = await CriarDeclaradoAsync(Declarado() with { Dominio = token });

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e => e.Field == "dominio" && e.Error.Code == codigo);
    }

    [Theory(DisplayName = "Derivado: domínio vazio é obrigatório e desconhecido é inválido")]
    [MemberData(nameof(Dominios))]
    public async Task Derivado_Dominio(string token, string codigo)
    {
        Result<Guid> resultado = await CriarDerivadoAsync(Derivado() with { Dominio = token });

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e => e.Field == "dominio" && e.Error.Code == codigo);
    }

    [Fact(DisplayName = "Declarado: cardinalidade vazia é obrigatória e desconhecida é inválida")]
    public async Task Declarado_Cardinalidade()
    {
        Result<Guid> vazia = await CriarDeclaradoAsync(Declarado() with { Cardinalidade = " " });
        Result<Guid> desconhecida = await CriarDeclaradoAsync(Declarado() with { Cardinalidade = "MUITOS" });

        vazia.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeObrigatoria);
        desconhecida.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeInvalida);
    }

    [Fact(DisplayName = "Token com espaços nas pontas é o token aparado")]
    public async Task Declarado_TokenComEspacos_Aceito()
    {
        Result<Guid> resultado = await CriarDeclaradoAsync(
            Declarado() with { Dominio = " BOOLEANO ", Cardinalidade = " ESCALAR ", Escopo = " CANDIDATO ", ClassificacaoProtecao = " PESSOAL " });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Declarado: fonte e formato vazios são não informados, e escopo vazio ou desconhecido é recusado")]
    public async Task Declarado_FonteFormatoEscopo()
    {
        Result<Guid> vazios = await CriarDeclaradoAsync(Declarado() with { FonteValores = "  ", Formato = "" });
        Result<Guid> escopoVazio = await CriarDeclaradoAsync(Declarado() with { Escopo = "" });
        Result<Guid> escopoDesconhecido = await CriarDeclaradoAsync(Declarado() with { Escopo = "TODOS" });

        vazios.IsSuccess.Should().BeTrue(vazios.Error?.Message);
        escopoVazio.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.EscopoObrigatorio);
        escopoDesconhecido.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.EscopoObrigatorio);
    }

    private static CriarFatoCandidatoCommand Declarado() => new(
        "FATO_NOVO", "Fato novo", null, "BOOLEANO", "ESCALAR", null, null, "INSCRICAO", "CANDIDATO", "PESSOAL", Finalidade,
        "CUMPRIMENTO_OBRIGACAO_LEGAL");

    private static CriarFatoDerivadoCommand Derivado() => new(
        "DERIVADO_NOVO", "Derivado novo", null, "BOOLEANO", "INSCRICAO", "CANDIDATO", "PESSOAL", Finalidade, "CUMPRIMENTO_OBRIGACAO_LEGAL");

    private static CriarFatoAgregadoCommand Agregado() => new(
        "AGREGADO_NOVO", "Agregado novo", null, "MEMBRO", "INSCRICAO", "PESSOAL", Finalidade, "CUMPRIMENTO_OBRIGACAO_LEGAL");

    private Task<Result<Guid>> CriarDeclaradoAsync(CriarFatoCandidatoCommand comando) =>
        CriarFatoCandidatoCommandHandler.Handle(comando, _fatos, _unitOfWork, CancellationToken.None);

    private Task<Result<Guid>> CriarDerivadoAsync(CriarFatoDerivadoCommand comando) =>
        CriarFatoDerivadoCommandHandler.Handle(comando, _fatos, _unitOfWork, CancellationToken.None);

    private Task<Result<Guid>> CriarAgregadoAsync(CriarFatoAgregadoCommand comando) =>
        CriarFatoAgregadoCommandHandler.Handle(comando, _fatos, _precedencias, _unitOfWork, CancellationToken.None);
}
