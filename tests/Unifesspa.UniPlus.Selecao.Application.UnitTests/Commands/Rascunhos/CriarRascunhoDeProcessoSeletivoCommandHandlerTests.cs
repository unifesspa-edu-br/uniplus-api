namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Rascunhos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

public sealed class CriarRascunhoDeProcessoSeletivoCommandHandlerTests
{
    private readonly CenarioDoRascunho _cenario = new();

    private Task<Result<RascunhoSalvoDto>> Criar(string documento) =>
        CriarRascunhoDeProcessoSeletivoCommandHandler.Handle(
            new CriarRascunhoDeProcessoSeletivoCommand(documento),
            _cenario.Preparacao,
            _cenario.UnidadesDoDocumento,
            _cenario.Rascunhos,
            _cenario.UnitOfWork,
            _cenario.Diagnostico,
            CancellationToken.None);

    private async Task NadaFoiGravado()
    {
        await _cenario.Rascunhos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await _cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "O documento válido cria o rascunho na revisão 1, na unidade do documento, e devolve o id, o ETag e nenhum diagnóstico")]
    public async Task DocumentoValido_CriaNaRevisao1()
    {
        RascunhoDeProcessoSeletivo? criado = null;
        await _cenario.Rascunhos.AdicionarAsync(Arg.Do<RascunhoDeProcessoSeletivo>(r => criado = r), Arg.Any<CancellationToken>());

        Result<RascunhoSalvoDto> resultado = await Criar(CenarioDoRascunho.DocumentoValido());

        resultado.IsSuccess.Should().BeTrue();
        criado.Should().NotBeNull();
        criado!.UnidadeAdministradoraOrigemId.Should().Be(CenarioDoRascunho.Ceps.Id);
        criado.Nome.Should().Be("Processo Seletivo de Exemplo");
        resultado.Value!.Id.Should().Be(criado.Id);
        resultado.Value.Revisao.Should().Be(1);
        resultado.Value.ETag.Should().Be("\"1\"");
        resultado.Value.Diagnosticos.Should().BeEmpty();
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O documento com exigência em formulário que o processo ainda não tem cria o rascunho e não traz diagnóstico: a falta do formulário só é recusada na publicação")]
    public async Task ExigenciaEmFormularioAindaNaoCriado_CriaORascunhoSemDiagnostico()
    {
        RascunhoDeProcessoSeletivo? criado = null;
        await _cenario.Rascunhos.AdicionarAsync(Arg.Do<RascunhoDeProcessoSeletivo>(r => criado = r), Arg.Any<CancellationToken>());
        string documento = CenarioDoRascunho.DocumentoValido().Replace(
            "\"versaoFormato\": 1,",
            "\"versaoFormato\": 1, \"documentosExigidos\": { \"exigencias\": [ { \"chave\": \"exigencia_comprovante\", \"obrigatorio\": true, \"abrePrazoParaReenvio\": false, \"formularioCodigo\": \"FORMULARIO_QUE_AINDA_NAO_EXISTE\" } ] },",
            StringComparison.Ordinal);

        Result<RascunhoSalvoDto> resultado = await Criar(documento);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Diagnosticos.Should().BeEmpty();
        criado!.Documento.Should().Contain("FORMULARIO_QUE_AINDA_NAO_EXISTE");
    }

    [Fact(DisplayName = "O documento guardado é o normalizado: sem o que é só de leitura")]
    public async Task DocumentoGuardado_EONormalizado()
    {
        RascunhoDeProcessoSeletivo? criado = null;
        await _cenario.Rascunhos.AdicionarAsync(Arg.Do<RascunhoDeProcessoSeletivo>(r => criado = r), Arg.Any<CancellationToken>());
        string documento = CenarioDoRascunho.DocumentoValido().Replace("\"versaoFormato\": 1,", "\"versaoFormato\": 1, \"id\": \"outro\",", StringComparison.Ordinal);

        await Criar(documento);

        criado!.Documento.Should().NotContain("\"id\"");
    }

    [Fact(DisplayName = "A forma inválida recusa com o grupo forma_invalida na frente das causas e não grava")]
    public async Task FormaInvalida_GrupoNaFrenteDasCausas()
    {
        _cenario.Esquema.Avaliar(Arg.Any<System.Text.Json.Nodes.JsonObject>()).Returns(
            [new FieldError("/identificacao/nome", new DomainError(DocumentoDaConfiguracaoErrorCodes.TipoInvalido, "Tipo."))]);

        Result<RascunhoSalvoDto> resultado = await Criar(CenarioDoRascunho.DocumentoValido());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            (null, DocumentoDaConfiguracaoErrorCodes.FormaInvalida),
            ("/identificacao/nome", DocumentoDaConfiguracaoErrorCodes.TipoInvalido));
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "O documento acima do limite recusa com o limite como raiz, sem o grupo da forma")]
    public async Task AcimaDoLimite_SemGrupoDaForma()
    {
        string enorme = new('a', 2 * 1024 * 1024 + 1);

        Result<RascunhoSalvoDto> resultado = await Criar(enorme);

        resultado.Error!.Code.Should().Be(DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite);
        resultado.Errors.Should().HaveCount(1);
    }

    [Fact(DisplayName = "O JSON malformado recusa com um só grupo forma_invalida")]
    public async Task Malformado_UmSoGrupo()
    {
        Result<RascunhoSalvoDto> resultado = await Criar("{ não é json");

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(DocumentoDaConfiguracaoErrorCodes.FormaInvalida);
    }

    [Fact(DisplayName = "O documento sem a unidade administradora recusa com o campo apontado, antes de consultar unidade ou acesso")]
    public async Task SemUnidade_Recusa422()
    {
        Result<RascunhoSalvoDto> resultado = await Criar("""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1 }""");

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente);
        resultado.Errors[0].Field.Should().Be("/identificacao/unidadeAdministradoraCodigo");
        await _cenario.Unidades.DidNotReceiveWithAnyArgs().ObterPorCodigoAsync(default!, default);
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "O código sem unidade viva e a unidade sem permissão recusam com a mesma resposta")]
    public async Task CodigoInexistenteESemPermissao_MesmaRecusa()
    {
        _cenario.NaoPodeConfigurarEm(CenarioDoRascunho.Ceps);
        Result<RascunhoSalvoDto> semPermissao = await Criar(CenarioDoRascunho.DocumentoValido());
        Result<RascunhoSalvoDto> inexistente = await Criar(CenarioDoRascunho.DocumentoValido("NAOEXISTE"));

        semPermissao.Error.Should().BeEquivalentTo(inexistente.Error);
        semPermissao.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao);
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "A permissão é decidida sobre a unidade do documento e sobre o processo a criar")]
    public async Task Permissao_DecididaNaUnidadeDoDocumento()
    {
        await Criar(CenarioDoRascunho.DocumentoValido(CenarioDoRascunho.Cga.Codigo));

        await _cenario.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == "selecao:processos:configurar"),
            Arg.Is<ResourceContext?>(r => r!.UnidadeProprietariaId == CenarioDoRascunho.Cga.Id && r.ProcessoId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A conferência da concorrência na gravação descarta o rastreamento e devolve o conflito")]
    public async Task ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException());

        Result<RascunhoSalvoDto> resultado = await Criar(CenarioDoRascunho.DocumentoValido());

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia);
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "O município que o Geo não conhece cria o rascunho do mesmo modo e volta como aviso no diagnóstico")]
    public async Task MunicipioDesconhecidoDoGeo_CriaComAviso()
    {
        Result<RascunhoSalvoDto> resultado = await Criar(CenarioDoRascunho.DocumentoValido(provaPresencial: CenarioDoRascunho.ProvaPresencialNoPara("1599999")));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Diagnosticos.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("/provaPresencial/municipios/0/codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo));
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com o Geo fora do ar o rascunho é criado e o diagnóstico traz um só aviso de Geo indisponível")]
    public async Task GeoForaDoAr_CriaComUmSoAviso()
    {
        _cenario.GeoForaDoAr();

        Result<RascunhoSalvoDto> resultado = await Criar(CenarioDoRascunho.DocumentoValido(provaPresencial: CenarioDoRascunho.ProvaPresencialNoPara("1504208", "1501402")));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Diagnosticos.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("/provaPresencial/municipios", ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel));
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O Geo é consultado uma vez por município, depois da permissão e antes de gravar o rascunho")]
    public async Task Geo_ConsultadoDepoisDaPermissaoEAntesDeGravar()
    {
        _cenario.GeoConhece("1504208", "Marabá");

        await Criar(CenarioDoRascunho.DocumentoValido(provaPresencial: CenarioDoRascunho.ProvaPresencialNoPara("1504208")));

        Received.InOrder(() =>
        {
            _ = _cenario.Verificador.ExigirAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
            _ = _cenario.Geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>());
            _ = _cenario.Rascunhos.AdicionarAsync(Arg.Any<RascunhoDeProcessoSeletivo>(), Arg.Any<CancellationToken>());
        });
        await _cenario.Geo.Received(1).ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A pessoa sem permissão na unidade é recusada antes de qualquer consulta ao Geo")]
    public async Task SemPermissao_NaoConsultaOGeo()
    {
        _cenario.NaoPodeConfigurarEm(CenarioDoRascunho.Ceps);

        Result<RascunhoSalvoDto> resultado = await Criar(CenarioDoRascunho.DocumentoValido(provaPresencial: CenarioDoRascunho.ProvaPresencialNoPara("1504208")));

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao);
        await _cenario.Geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }

    [Fact(DisplayName = "A forma inválida é recusada antes de qualquer consulta ao Geo")]
    public async Task FormaInvalida_NaoConsultaOGeo()
    {
        _cenario.Esquema.Avaliar(Arg.Any<System.Text.Json.Nodes.JsonObject>()).Returns(
            [new FieldError("/provaPresencial/municipios", new DomainError(DocumentoDaConfiguracaoErrorCodes.TipoInvalido, "Tipo."))]);

        await Criar(CenarioDoRascunho.DocumentoValido(provaPresencial: CenarioDoRascunho.ProvaPresencialNoPara("1504208")));

        await _cenario.Geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Os argumentos nulos são recusados")]
    public async Task ArgumentosNulos_Lancam()
    {
        Func<Task> semComando = () => CriarRascunhoDeProcessoSeletivoCommandHandler.Handle(
            null!, _cenario.Preparacao, _cenario.UnidadesDoDocumento, _cenario.Rascunhos, _cenario.UnitOfWork, _cenario.Diagnostico, CancellationToken.None);

        await semComando.Should().ThrowAsync<ArgumentNullException>();
    }
}
