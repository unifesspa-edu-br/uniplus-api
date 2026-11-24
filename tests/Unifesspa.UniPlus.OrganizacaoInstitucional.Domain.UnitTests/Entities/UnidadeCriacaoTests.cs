namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class UnidadeCriacaoTests
{
    [Fact(DisplayName = "Criar com dados válidos retorna Success com agregado preenchido")]
    public void Criar_ComDadosValidos_DeveRetornarSuccessComAgregadoPreenchido()
    {
        Result<Unidade> resultado = UnidadeDeTeste.Criar();

        resultado.IsSuccess.Should().BeTrue();
        Unidade u = resultado.Value!;
        u.Nome.Should().Be("Centro de Processos Seletivos");
        u.Slug.Should().Be(UnidadeDeTeste.SlugDoCeps);
        u.Sigla.Should().Be("CEPS");
        u.Codigo.Should().Be("0001");
        u.Tipo.Should().Be(TipoUnidade.Centro);
        u.Id.Should().NotBeEmpty("ADR-0032 exige Guid v7 via EntityBase");
        u.IsDeleted.Should().BeFalse();
    }

    [Theory(DisplayName = "Criar com nome vazio/whitespace retorna NomeObrigatorio")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComNomeVazio_DeveRetornarNomeObrigatorio(string nomeInvalido)
    {
        Result<Unidade> resultado = Unidade.Criar(
            nomeInvalido,
            UnidadeDeTeste.IdentificadoresDoCeps(),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Criar com nome com 1 char retorna NomeTamanho (limite inferior)")]
    public void Criar_ComNomeMuitoCurto_DeveRetornarNomeTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "a",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NomeTamanho);
    }

    [Theory(DisplayName = "Criar com tipo que não é membro de TipoUnidade retorna TipoInvalido")]
    [InlineData((TipoUnidade)0)]
    [InlineData((TipoUnidade)99)]
    public void Criar_ComTipoInvalido_DeveRetornarTipoInvalido(TipoUnidade tipoInvalido)
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            tipoInvalido,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.TipoInvalido);
    }

    [Fact(DisplayName = "Criar com vigenciaFim anterior ao início retorna VigenciaFimAnteriorAoInicio")]
    public void Criar_ComVigenciaFimAntesDoInicio_DeveRetornarVigenciaFimAnteriorAoInicio()
    {
        DateOnly fimAnterior = UnidadeDeTeste.InicioDaVigencia.AddDays(-1);

        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, fimAnterior),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.VigenciaFimAnteriorAoInicio);
    }

    [Fact(DisplayName = "Criar com três violações independentes acumula as três no mesmo lote")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "",
            new IdentificadoresDaUnidade("SLUG-INVALIDO", "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            (TipoUnidade)99,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors.Should().Contain(e => e.Field == "nome" && e.Error.Code == UnidadeErrorCodes.NomeObrigatorio);
        resultado.Errors.Should().Contain(e => e.Field == "slug" && e.Error.Code == UnidadeErrorCodes.SlugFormatoInvalido);
        resultado.Errors.Should().Contain(e => e.Field == "tipo" && e.Error.Code == UnidadeErrorCodes.TipoInvalido);
    }

    [Fact(DisplayName = "ValidarCampos replica o resultado de Criar sem instanciar o agregado")]
    public void ValidarCampos_MesmoPayloadDeCriarComFalha_RetornaMesmosErros()
    {
        Result validacao = Unidade.ValidarCampos(
            null,
            new IdentificadoresDaUnidade(null, "CEPS", "0001", null),
            new CidadeDaUnidade(null, null, null),
            TipoUnidade.Centro,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null));

        validacao.IsFailure.Should().BeTrue();
        validacao.Errors.Should().ContainSingle(e => e.Field == "nome" && e.Error.Code == UnidadeErrorCodes.NomeObrigatorio);
        validacao.Errors.Should().ContainSingle(e => e.Field == "slug" && e.Error.Code == UnidadeErrorCodes.SlugObrigatorio);
    }

    [Fact(DisplayName = "ValidarCampos sem tipo recusa com TipoInvalido e lista os códigos aceitos")]
    public void ValidarCampos_SemTipo_RecusaListandoOsCodigosAceitos()
    {
        Result validacao = Unidade.ValidarCampos(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            CidadeDaUnidade.Nenhuma,
            null,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null));

        FieldError erro = validacao.Errors.Should().ContainSingle(e => e.Field == "tipo").Subject;
        erro.Error.Code.Should().Be(UnidadeErrorCodes.TipoInvalido);
        erro.Error.Message.Should().Contain("REITORIA").And.Contain("PRO_REITORIA").And.Contain("OUTRO");
    }
}
