namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

public sealed class UnidadeTests
{
    private static readonly DateOnly DataInicio = new(2026, 1, 1);
    private static readonly Slug SlugValido = Slug.From("ceps").Value!;

    private static Unidade CriarUnidadeValida(string? alias = null, Guid? superiorId = null) =>
        Unidade.Criar(
            "Centro de Processos Seletivos",
            alias,
            SlugValido,
            "CEPS",
            "0001",
            superiorId,
            TipoUnidade.Centro,
            false,
            DataInicio,
            null,
            OrigemUnidade.CriadoNoUniPlus).Value!;

    // ── Criar ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Criar com dados válidos retorna Success com agregado preenchido")]
    public void Criar_ComDadosValidos_DeveRetornarSuccessComAgregadoPreenchido()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Centro de Processos Seletivos",
            null,
            SlugValido,
            "CEPS",
            "0001",
            null,
            TipoUnidade.Centro,
            false,
            DataInicio,
            null,
            OrigemUnidade.CriadoNoUniPlus);

        resultado.IsSuccess.Should().BeTrue();
        Unidade u = resultado.Value!;
        u.Nome.Should().Be("Centro de Processos Seletivos");
        u.Slug.Should().Be(SlugValido);
        u.Sigla.Should().Be("CEPS");
        u.Codigo.Should().Be("0001");
        u.Tipo.Should().Be(TipoUnidade.Centro);
        u.Origem.Should().Be(OrigemUnidade.CriadoNoUniPlus);
        u.Id.Should().NotBeEmpty("ADR-0032 exige Guid v7 via EntityBase");
        u.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar abre histórico inicial para Slug, Sigla e Codigo")]
    public void Criar_ComDadosValidos_AbreHistoricoInicialParaIdentificadores()
    {
        Unidade unidade = CriarUnidadeValida();

        unidade.Historico.Should().HaveCount(3);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug && h.Valor == "ceps" && h.VigenciaFim == null);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Sigla && h.Valor == "CEPS" && h.VigenciaFim == null);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Codigo && h.Valor == "0001" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Criar com alias abre histórico adicional para Alias")]
    public void Criar_ComAlias_AbreHistoricoParaAlias()
    {
        Unidade unidade = CriarUnidadeValida(alias: "Centro Seletivos");

        unidade.Historico.Should().HaveCount(4);
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Alias && h.Valor == "Centro Seletivos" && h.VigenciaFim == null);
    }

    [Theory(DisplayName = "Criar com nome vazio/whitespace retorna NomeObrigatorio")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComNomeVazio_DeveRetornarNomeObrigatorio(string nomeInvalido)
    {
        Result<Unidade> resultado = Unidade.Criar(
            nomeInvalido, null, SlugValido, "CEPS", "0001", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Criar com nome com 1 char retorna NomeTamanho (limite inferior)")]
    public void Criar_ComNomeMuitoCurto_DeveRetornarNomeTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "a", null, SlugValido, "CEPS", "0001", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Criar com sigla vazia retorna SiglaObrigatoria")]
    public void Criar_ComSiglaVazia_DeveRetornarSiglaObrigatoria()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", null, SlugValido, "  ", "0001", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaObrigatoria);
    }

    [Fact(DisplayName = "Criar com sigla > 50 chars retorna SiglaTamanho")]
    public void Criar_ComSiglaMuitoLonga_DeveRetornarSiglaTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", null, SlugValido, new string('A', 51), "0001", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaTamanho);
    }

    [Fact(DisplayName = "Criar com código vazio retorna CodigoObrigatorio")]
    public void Criar_ComCodigoVazio_DeveRetornarCodigoObrigatorio()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", null, SlugValido, "CEPS", "  ", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "Criar com alias > 100 chars retorna AliasTamanho")]
    public void Criar_ComAliasMuitoLongo_DeveRetornarAliasTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", new string('a', 101), SlugValido, "CEPS", "0001", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.AliasTamanho);
    }

    [Theory(DisplayName = "Criar com TipoUnidade.Nenhum ou cast inválido retorna TipoInvalido")]
    [InlineData(TipoUnidade.Nenhum)]
    [InlineData((TipoUnidade)99)]
    public void Criar_ComTipoInvalido_DeveRetornarTipoInvalido(TipoUnidade tipoInvalido)
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", null, SlugValido, "CEPS", "0001", null,
            tipoInvalido, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.TipoInvalido);
    }

    [Theory(DisplayName = "Criar com OrigemUnidade.Nenhum ou cast inválido retorna OrigemInvalida")]
    [InlineData(OrigemUnidade.Nenhum)]
    [InlineData((OrigemUnidade)99)]
    public void Criar_ComOrigemInvalida_DeveRetornarOrigemInvalida(OrigemUnidade origemInvalida)
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", null, SlugValido, "CEPS", "0001", null,
            TipoUnidade.Centro, false, DataInicio, null, origemInvalida);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.OrigemInvalida);
    }

    [Fact(DisplayName = "Criar com vigenciaFim anterior ao início retorna VigenciaFimAnteriorAoInicio")]
    public void Criar_ComVigenciaFimAntesDoInicio_DeveRetornarVigenciaFimAnteriorAoInicio()
    {
        DateOnly fimAnterior = DataInicio.AddDays(-1);

        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", null, SlugValido, "CEPS", "0001", null,
            TipoUnidade.Centro, false, DataInicio, fimAnterior, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.VigenciaFimAnteriorAoInicio);
    }

    // ── Atualizar ──────────────────────────────────────────────────────

    [Fact(DisplayName = "Atualizar com dados válidos altera campos e não toca histórico sem mudança de identificadores")]
    public void Atualizar_SemMudancaDeIdentificadores_NaoAdicionaHistorico()
    {
        Unidade unidade = CriarUnidadeValida();
        int historicoAntes = unidade.Historico.Count;
        DateOnly dataAtual = DataInicio.AddMonths(1);

        Result resultado = unidade.Atualizar(
            "Centro de Processos Seletivos Alterado",
            null, SlugValido, "CEPS", "0001", null,
            TipoUnidade.Centro, true, null, dataAtual);

        resultado.IsSuccess.Should().BeTrue();
        unidade.Nome.Should().Be("Centro de Processos Seletivos Alterado");
        unidade.UnidadeAcademica.Should().BeTrue();
        unidade.Historico.Should().HaveCount(historicoAntes, "nenhum identificador mudou");
    }

    [Fact(DisplayName = "Atualizar com novo Slug fecha vigência antiga e abre nova entrada de histórico")]
    public void Atualizar_ComNovoSlug_AdicionaEntradaDeHistorico()
    {
        Unidade unidade = CriarUnidadeValida();
        Slug novoSlug = Slug.From("ceps-unifesspa").Value!;
        DateOnly dataAtual = DataInicio.AddMonths(1);

        Result resultado = unidade.Atualizar(
            unidade.Nome, null, novoSlug, "CEPS", "0001", null,
            TipoUnidade.Centro, false, null, dataAtual, "Adequação de naming");

        resultado.IsSuccess.Should().BeTrue();
        unidade.Slug.Should().Be(novoSlug);

        // Entrada antiga fechada
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug &&
            h.Valor == "ceps" &&
            h.VigenciaFim == dataAtual);

        // Nova entrada aberta
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug &&
            h.Valor == "ceps-unifesspa" &&
            h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Atualizar troca de Codigo só na caixa (ABC→abc) registra histórico")]
    public void Atualizar_ComCodigoMudandoApenasCaixa_AdicionaEntradaDeHistorico()
    {
        Unidade unidade = Unidade.Criar(
            "Centro de Processos Seletivos", null, SlugValido, "CEPS", "ABC", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus).Value!;
        DateOnly dataAtual = DataInicio.AddMonths(1);

        Result resultado = unidade.Atualizar(
            unidade.Nome, null, SlugValido, "CEPS", "abc", null,
            TipoUnidade.Centro, false, null, dataAtual, "Correção de caixa");

        resultado.IsSuccess.Should().BeTrue();
        unidade.Codigo.Should().Be("abc");

        // Entrada antiga "ABC" fechada na data da mudança.
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Codigo &&
            h.Valor == "ABC" && h.VigenciaFim == dataAtual);
        // Nova entrada "abc" aberta.
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Codigo &&
            h.Valor == "abc" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Sigla é normalizada para uppercase no Criar")]
    public void Criar_SiglaEhNormalizadaParaUppercase()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido", null, SlugValido, "ceps", "0001", null,
            TipoUnidade.Centro, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sigla.Should().Be("CEPS");
    }
}
