namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class SubstituirRascunhoDeProcessoSeletivoCommandHandlerTests
{
    private readonly CenarioDoRascunho _cenario = new();

    private Task<Result<RascunhoSalvoDto>> Substituir(Guid id, string documento, PrecondicaoIfMatch precondicao) =>
        SubstituirRascunhoDeProcessoSeletivoCommandHandler.Handle(
            new SubstituirRascunhoDeProcessoSeletivoCommand(id, documento, precondicao),
            _cenario.Preparacao,
            _cenario.UnidadesDoDocumento,
            _cenario.Bloqueio,
            _cenario.UnitOfWork,
            CancellationToken.None);

    private async Task NadaFoiGravado()
    {
        await _cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Com o ETag da revisão atual troca o documento, sobe a revisão e devolve o novo ETag sem diagnósticos")]
    public async Task ETagQueCasa_SobeARevisao()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            rascunho.Id, CenarioDoRascunho.DocumentoValido(nome: "Segundo nome"), CenarioDoRascunho.Precondicao(rascunho));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Revisao.Should().Be(2);
        resultado.Value.ETag.Should().Be("\"2\"");
        resultado.Value.Diagnosticos.Should().BeEmpty();
        rascunho.Nome.Should().Be("Segundo nome");
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem If-Match recusa com 428 antes de ler o documento, a unidade ou bloquear a linha")]
    public async Task SemIfMatch_428AntesDeTudo()
    {
        Result<RascunhoSalvoDto> resultado = await Substituir(Guid.CreateVersion7(), "{ não é json", PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Requerida);
        await _cenario.Unidades.DidNotReceiveWithAnyArgs().ObterPorCodigoAsync(default!, default);
        await _cenario.Rascunhos.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
    }

    [Fact(DisplayName = "A forma inválida recusa com todas as causas antes de bloquear a linha, mesmo com ETag obsoleto")]
    public async Task FormaInvalida_AntesDoBloqueio()
    {
        _cenario.Esquema.Avaliar(Arg.Any<JsonObject>()).Returns(
            [new FieldError("/identificacao/nome", new DomainError(DocumentoDaConfiguracaoErrorCodes.TipoInvalido, "Tipo."))]);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            Guid.CreateVersion7(), CenarioDoRascunho.DocumentoValido(), PrecondicaoIfMatch.DeTags(["\"9\""]));

        resultado.Errors[0].Error.Code.Should().Be(DocumentoDaConfiguracaoErrorCodes.FormaInvalida);
        await _cenario.Rascunhos.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
    }

    [Fact(DisplayName = "O documento sem unidade recusa 422 antes de bloquear a linha")]
    public async Task SemUnidade_AntesDoBloqueio()
    {
        Result<RascunhoSalvoDto> resultado = await Substituir(
            Guid.CreateVersion7(), """{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1 }""", PrecondicaoIfMatch.Curinga);

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente);
        await _cenario.Rascunhos.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
    }

    [Fact(DisplayName = "A linha ausente com processo de mesmo id recusa 409 ja_publicado")]
    public async Task LinhaAusenteComProcesso_JaPublicado()
    {
        Guid id = Guid.CreateVersion7();
        _cenario.Processos.ExisteAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        Result<RascunhoSalvoDto> resultado = await Substituir(id, CenarioDoRascunho.DocumentoValido(), PrecondicaoIfMatch.Curinga);

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.JaPublicado);
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "A linha ausente sem processo recusa 404")]
    public async Task LinhaAusenteSemProcesso_NaoEncontrado()
    {
        Result<RascunhoSalvoDto> resultado = await Substituir(
            Guid.CreateVersion7(), CenarioDoRascunho.DocumentoValido(), PrecondicaoIfMatch.Curinga);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "A unidade que a linha tem agora, onde a pessoa não configura, recusa 404 e nada muda")]
    public async Task UnidadeAtualSemPermissao_404ENadaMuda()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Cga);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            rascunho.Id, CenarioDoRascunho.DocumentoValido(nome: "Outro nome"), CenarioDoRascunho.Precondicao(rascunho));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        rascunho.UnidadeAdministradoraOrigemId.Should().Be(CenarioDoRascunho.Cga.Id);
        rascunho.Revisao.Should().Be(1);
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "A unidade atual sem permissão recusa 404 antes do 412 e antes de decidir a unidade nova")]
    public async Task UnidadeAtualSemPermissao_404AntesDe412E403()
    {
        _cenario.PodeConfigurarEm(CenarioDoRascunho.Ceps);
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Cga);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            rascunho.Id, CenarioDoRascunho.DocumentoValido(CenarioDoRascunho.Ceps.Codigo), PrecondicaoIfMatch.DeTags(["\"7\""]));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        _cenario.DecisoesSobreAUnidade(CenarioDoRascunho.Cga).Should().Be(1);
        _cenario.DecisoesSobreAUnidade(CenarioDoRascunho.Ceps).Should().Be(0);
    }

    [Fact(DisplayName = "O ETag obsoleto recusa 412 antes de decidir o acesso à unidade nova, e nada muda")]
    public async Task ETagObsoleto_412AntesDa403()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            rascunho.Id, CenarioDoRascunho.DocumentoValido(CenarioDoRascunho.Cga.Codigo), PrecondicaoIfMatch.DeTags(["\"7\""]));

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
        _cenario.DecisoesSobreAUnidade(CenarioDoRascunho.Cga).Should().Be(0);
        rascunho.Revisao.Should().Be(1);
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "A troca para unidade onde a pessoa não configura recusa 403 e mantém a unidade e a revisão")]
    public async Task TrocaSemPermissaoNaNova_403()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            rascunho.Id, CenarioDoRascunho.DocumentoValido(CenarioDoRascunho.Cga.Codigo), CenarioDoRascunho.Precondicao(rascunho));

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao);
        rascunho.UnidadeAdministradoraOrigemId.Should().Be(CenarioDoRascunho.Ceps.Id);
        rascunho.Revisao.Should().Be(1);
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "O código sem unidade viva recusa 403 como a unidade sem permissão")]
    public async Task CodigoInexistente_MesmaRecusaDaFaltaDePermissao()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            rascunho.Id, CenarioDoRascunho.DocumentoValido("NAOEXISTE"), CenarioDoRascunho.Precondicao(rascunho));

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao);
        await NadaFoiGravado();
    }

    [Fact(DisplayName = "A troca para unidade onde a pessoa também configura muda a unidade dona e sobe a revisão")]
    public async Task TrocaComPermissaoNasDuas_MudaAUnidade()
    {
        _cenario.PodeConfigurarEm(CenarioDoRascunho.Cga);
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result<RascunhoSalvoDto> resultado = await Substituir(
            rascunho.Id, CenarioDoRascunho.DocumentoValido(CenarioDoRascunho.Cga.Codigo), CenarioDoRascunho.Precondicao(rascunho));

        resultado.IsSuccess.Should().BeTrue();
        rascunho.UnidadeAdministradoraOrigemId.Should().Be(CenarioDoRascunho.Cga.Id);
        rascunho.Revisao.Should().Be(2);
    }

    [Fact(DisplayName = "Salvar na mesma unidade decide o acesso uma vez só, o da unidade atual da linha bloqueada")]
    public async Task MesmaUnidade_NaoDecideDeNovo()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        await Substituir(rascunho.Id, CenarioDoRascunho.DocumentoValido(), CenarioDoRascunho.Precondicao(rascunho));

        _cenario.DecisoesSobreAUnidade(CenarioDoRascunho.Ceps).Should().Be(1);
    }

    [Fact(DisplayName = "A linha é bloqueada depois da forma e da unidade resolvidas, e a unidade nova é lida fora do bloqueio")]
    public async Task OrdemDasLeituras_UnidadeAntesDoBloqueio()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        await Substituir(rascunho.Id, CenarioDoRascunho.DocumentoValido(), CenarioDoRascunho.Precondicao(rascunho));

        Received.InOrder(() =>
        {
            _ = _cenario.Unidades.ObterPorCodigoAsync(CenarioDoRascunho.CodigoDaUnidade, Arg.Any<CancellationToken>());
            _ = _cenario.Rascunhos.ObterParaMutacaoAsync(rascunho.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "A exceção de concorrência não é capturada: sem Idempotency-Key, o tratamento é o central")]
    public async Task ExcecaoDeConcorrencia_Propaga()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException());

        Func<Task> substituicao = () => Substituir(rascunho.Id, CenarioDoRascunho.DocumentoValido(), CenarioDoRascunho.Precondicao(rascunho));

        await substituicao.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>();
    }

    [Fact(DisplayName = "Os argumentos nulos são recusados")]
    public async Task ArgumentosNulos_Lancam()
    {
        Func<Task> semComando = () => SubstituirRascunhoDeProcessoSeletivoCommandHandler.Handle(
            null!, _cenario.Preparacao, _cenario.UnidadesDoDocumento, _cenario.Bloqueio, _cenario.UnitOfWork, CancellationToken.None);

        await semComando.Should().ThrowAsync<ArgumentNullException>();
    }
}
