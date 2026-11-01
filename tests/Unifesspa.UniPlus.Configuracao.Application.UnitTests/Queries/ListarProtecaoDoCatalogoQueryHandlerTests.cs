namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>O que aguarda a proteção de dados, visto por quem aprova e por quem mantém cada cadastro (ADR-0153).</summary>
public sealed class ListarProtecaoDoCatalogoQueryHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();

    public ListarProtecaoDoCatalogoQueryHandlerTests()
    {
        TipoDocumento solicitado = NovoTipo();
        solicitado.SolicitarAprovacaoDaProtecao(Isabella, Agora);
        _tipos.ListarSemProtecaoAprovadaAsync(Arg.Any<CancellationToken>()).Returns([solicitado, NovoTipo("COMPROVANTE_RESIDENCIA", "Comprovante de residência")]);
        _fatos.ListarSemProtecaoAprovadaAsync(Arg.Any<CancellationToken>()).Returns([NovoFato()]);
    }

    [Fact(DisplayName = "Quem aprova vê os dois cadastros, cada item com a situação dele")]
    public async Task Aprovador_VeOsDoisCadastros()
    {
        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await ListarAsync(null, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar);

        resultado.Value!.Select(static i => (i.Tipo, i.Codigo, i.Aprovacao.Situacao)).Should().Equal(
            ("TIPO_DOCUMENTO", "LAUDO_MEDICO_COMPLEMENTAR", "SOLICITADA"),
            ("TIPO_DOCUMENTO", "COMPROVANTE_RESIDENCIA", "PENDENTE"),
            ("FATO", "TEM_RENDA", "PENDENTE"));
    }

    [Fact(DisplayName = "Quem só mantém os fatos não vê os tipos de documento")]
    public async Task MantenedorDeFatos_VeSoOsFatos()
    {
        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await ListarAsync(null, PermissaoConfiguracao.FatosCandidatoManter);

        resultado.Value!.Should().ContainSingle().Which.Tipo.Should().Be("FATO");
        await _tipos.DidNotReceive().ListarSemProtecaoAprovadaAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O filtro por situação restringe a lista")]
    public async Task FiltraPorSituacao()
    {
        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await ListarAsync("SOLICITADA", PermissaoConfiguracao.ProtecaoDoCatalogoAprovar);

        resultado.Value!.Should().ContainSingle().Which.Codigo.Should().Be("LAUDO_MEDICO_COMPLEMENTAR");
    }

    [Theory(DisplayName = "A situação aprovada ou desconhecida não é filtro")]
    [InlineData("APROVADA")]
    [InlineData("QUALQUER")]
    public async Task SituacaoInvalida_Recusa(string situacao)
    {
        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await ListarAsync(situacao, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar);

        resultado.Errors.Should().ContainSingle(e => e.Field == "situacao").Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "Sem nenhuma das três permissões a consulta é recusada")]
    public async Task SemPermissao_Recusa()
    {
        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await ListarAsync(null, PermissaoConfiguracao.CursosManter);

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo);
    }

    private Task<Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>> ListarAsync(string? situacao, params PermissaoConfiguracao[] permissoes) =>
        ListarProtecaoDoCatalogoQueryHandler.Handle(
            new ListarProtecaoDoCatalogoQuery(situacao), _tipos, _fatos, VerificadorCom(permissoes), CancellationToken.None);
}
