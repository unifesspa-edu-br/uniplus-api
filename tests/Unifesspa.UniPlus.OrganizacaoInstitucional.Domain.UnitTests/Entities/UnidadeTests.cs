namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", alias),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            superiorId,
            TipoUnidade.Centro,
            false).Value!;

    // ── Criar ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Criar com dados válidos retorna Success com agregado preenchido")]
    public void Criar_ComDadosValidos_DeveRetornarSuccessComAgregadoPreenchido()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Centro de Processos Seletivos",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        Unidade u = resultado.Value!;
        u.Nome.Should().Be("Centro de Processos Seletivos");
        u.Slug.Should().Be(SlugValido);
        u.Sigla.Should().Be("CEPS");
        u.Codigo.Should().Be("0001");
        u.Tipo.Should().Be(TipoUnidade.Centro);
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
            nomeInvalido,
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
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
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Criar com sigla vazia retorna SiglaObrigatoria")]
    public void Criar_ComSiglaVazia_DeveRetornarSiglaObrigatoria()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "  ", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaObrigatoria);
    }

    [Fact(DisplayName = "Criar com sigla > 50 chars retorna SiglaTamanho")]
    public void Criar_ComSiglaMuitoLonga_DeveRetornarSiglaTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, new string('A', 51), "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaTamanho);
    }

    [Fact(DisplayName = "Criar com código vazio retorna CodigoObrigatorio")]
    public void Criar_ComCodigoVazio_DeveRetornarCodigoObrigatorio()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "  ", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "Criar com alias > 100 chars retorna AliasTamanho")]
    public void Criar_ComAliasMuitoLongo_DeveRetornarAliasTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", new string('a', 101)),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.AliasTamanho);
    }

    [Theory(DisplayName = "Criar com TipoUnidade.Nenhum ou cast inválido retorna TipoInvalido")]
    [InlineData(TipoUnidade.Nenhum)]
    [InlineData((TipoUnidade)99)]
    public void Criar_ComTipoInvalido_DeveRetornarTipoInvalido(TipoUnidade tipoInvalido)
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            tipoInvalido,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.TipoInvalido);
    }

    [Fact(DisplayName = "Criar com vigenciaFim anterior ao início retorna VigenciaFimAnteriorAoInicio")]
    public void Criar_ComVigenciaFimAntesDoInicio_DeveRetornarVigenciaFimAnteriorAoInicio()
    {
        DateOnly fimAnterior = DataInicio.AddDays(-1);

        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, fimAnterior),
            null,
            TipoUnidade.Centro,
            false);

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
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null), dataAtual, null),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            true);

        resultado.IsSuccess.Should().BeTrue();
        unidade.Nome.Should().Be("Centro de Processos Seletivos Alterado");
        unidade.UnidadeAcademica.Should().BeTrue();
        unidade.Historico.Should().HaveCount(historicoAntes, "nenhum identificador mudou");
    }

    [Fact(DisplayName = "Atualizar com motivo de mudança > 500 chars retorna MotivoMudancaTamanho sem alterar a Unidade")]
    public void Atualizar_ComMotivoMuitoLongo_DeveRetornarMotivoMudancaTamanho()
    {
        Unidade unidade = CriarUnidadeValida();

        Result resultado = unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade("ceps-unifesspa", "CEPS", "0001", null), DataInicio.AddMonths(1), new string('m', 501)),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "motivoMudancaIdentificador" && e.Error.Code == UnidadeErrorCodes.MotivoMudancaTamanho);
        unidade.Slug.Valor.Should().Be("ceps");
    }

    [Fact(DisplayName = "Atualizar com motivo de 500 chars mais espaços grava o histórico com 500 chars")]
    public void Atualizar_ComMotivoNoLimiteMaisEspacos_GravaMotivoAparado()
    {
        Unidade unidade = CriarUnidadeValida();

        Result resultado = unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade("ceps-unifesspa", "CEPS", "0001", null), DataInicio.AddMonths(1), new string('m', 500) + "   "),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        unidade.Historico.Single(h => h.TipoIdentificador == TipoIdentificador.Slug && h.VigenciaFim is null)
            .MotivoMudanca.Should().HaveLength(500);
    }

    [Fact(DisplayName = "Atualizar com novo Slug fecha vigência antiga e abre nova entrada de histórico")]
    public void Atualizar_ComNovoSlug_AdicionaEntradaDeHistorico()
    {
        Unidade unidade = CriarUnidadeValida();
        Slug novoSlug = Slug.From("ceps-unifesspa").Value!;
        DateOnly dataAtual = DataInicio.AddMonths(1);

        Result resultado = unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(novoSlug.Valor, "CEPS", "0001", null), dataAtual, "Adequação de naming"),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            false);

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
            "Centro de Processos Seletivos",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "ABC", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false).Value!;
        DateOnly dataAtual = DataInicio.AddMonths(1);

        Result resultado = unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "abc", null), dataAtual, "Correção de caixa"),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            false);

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
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "ceps", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sigla.Should().Be("CEPS");
    }

    // ── Cidade (issue #1114) — opcional all-or-nothing ────────────────────

    [Fact(DisplayName = "Criar sem cidade tem sucesso — cidade é opcional em Unidade")]
    public void Criar_SemCidade_Sucesso()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CidadeCodigoIbge.Should().BeNull();
        resultado.Value.CidadeNome.Should().BeNull();
        resultado.Value.CidadeUf.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com trio de cidade completo tem sucesso e normaliza UF para uppercase")]
    public void Criar_ComCidadeCompleta_Sucesso()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            new CidadeDaUnidade("1504208", "Marabá", "pa"),
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CidadeCodigoIbge.Should().Be("1504208");
        resultado.Value.CidadeNome.Should().Be("Marabá");
        resultado.Value.CidadeUf.Should().Be("PA");
    }

    [Theory(DisplayName = "Criar com cidade parcialmente preenchida falha — bicondicional all-or-nothing")]
    [InlineData(null, "Marabá", "PA")]
    [InlineData("1504208", null, "PA")]
    [InlineData("1504208", "Marabá", null)]
    public void Criar_ComCidadeParcial_Falha(string? codigo, string? nome, string? uf)
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            new CidadeDaUnidade(codigo, nome, uf),
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com código IBGE incoerente com a UF falha")]
    public void Criar_ComCidadeUfIncoerente_Falha()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            new CidadeDaUnidade("1504208", "Marabá", "SP"),
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Atualizar propaga cidade completa")]
    public void Atualizar_ComCidadeCompleta_Sucesso()
    {
        Unidade unidade = CriarUnidadeValida();
        DateOnly dataAtual = DataInicio.AddMonths(1);

        Result resultado = unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null), dataAtual, null),
            new CidadeDaUnidade("1504208", "Marabá", "PA"),
            null,
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        unidade.CidadeCodigoIbge.Should().Be("1504208");
        unidade.CidadeNome.Should().Be("Marabá");
        unidade.CidadeUf.Should().Be("PA");
    }

    [Fact(DisplayName = "Atualizar removendo cidade (trio nulo) tem sucesso e zera os três campos")]
    public void Atualizar_RemovendoCidade_ZeraOsTresCampos()
    {
        Unidade unidade = Unidade.Criar(
            "Centro de Processos Seletivos",
            new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null),
            new CidadeDaUnidade("1504208", "Marabá", "PA"),
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Centro,
            false).Value!;
        DateOnly dataAtual = DataInicio.AddMonths(1);

        Result resultado = unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(SlugValido.Valor, "CEPS", "0001", null), dataAtual, null),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        unidade.CidadeCodigoIbge.Should().BeNull();
        unidade.CidadeNome.Should().BeNull();
        unidade.CidadeUf.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com três violações independentes acumula as três no mesmo lote")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "",
            new IdentificadoresDaUnidade("SLUG-INVALIDO", "CEPS", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Nenhum,
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
            new VigenciaDaUnidade(DataInicio, null));

        validacao.IsFailure.Should().BeTrue();
        validacao.Errors.Should().ContainSingle(e => e.Field == "nome" && e.Error.Code == UnidadeErrorCodes.NomeObrigatorio);
        validacao.Errors.Should().ContainSingle(e => e.Field == "slug" && e.Error.Code == UnidadeErrorCodes.SlugObrigatorio);
    }
}
