namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaPublicacaoDoAtoAvulso;

/// <summary>
/// A ordem única das recusas do ato avulso: o instante, que sozinho encerra a resposta quando falha; a forma do pedido, o
/// estado do processo e as consultas, todos na mesma resposta quando não dependem uns dos outros.
/// </summary>
public sealed class FormaEOrdemDasRecusasDoAtoAvulsoTests
{
    private const string Obrigatorio = "uniplus.validacao.obrigatorio";

    private readonly CenarioDaPublicacaoDoAtoAvulso _cenario = new();

    [Fact(DisplayName = "O tipo, o documento principal e o ato corrigido inexistentes saem juntos, nessa ordem, e a raiz leva o código da primeira causa")]
    public async Task TipoDocumentoEAtoCorrigidoInexistentes_SaemJuntosNaOrdemDoPedido()
    {
        _cenario.Tipos.ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);
        PublicarAtoAvulsoDoProcessoCommand comando = _cenario.Comando(principalId: Guid.CreateVersion7(), atoCorrigidoId: Guid.CreateVersion7(), motivo: "Erro");

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Publicar(comando);

        resposta.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("tipoAtoCodigo", "ProcessoSeletivo.TipoDeAtoSemVersaoVigente"),
            ("documentoPrincipalId", AtoDoProcessoErrorCodes.DocumentoNaoEncontrado),
            ("atoCorrigidoId", AtoDoProcessoErrorCodes.AtoCorrigidoNaoEncontrado));
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
        mensagens.Should().BeEmpty();
        await _cenario.NadaFoiEscrito();
    }

    [Theory(DisplayName = "O assinante ausente, nulo, vazio ou em branco é recusado no campo dele como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssinanteAusente_RecusaNoCampoDele(string? assinante)
    {
        await _cenario.DeveRecusar(_cenario.Comando() with { Assinante = assinante }, Obrigatorio, "assinante");
    }

    [Theory(DisplayName = "O tipo ausente, nulo, vazio ou em branco é recusado no campo dele como obrigatório, sem consultar o catálogo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TipoAusente_RecusaNoCampoDeleSemConsultarOCatalogo(string? tipo)
    {
        await _cenario.DeveRecusar(_cenario.Comando() with { TipoAtoCodigo = tipo }, Obrigatorio, "tipoAtoCodigo");

        await _cenario.Tipos.DidNotReceive().ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo ausente não impede a conferência dos documentos nem a do ato corrigido")]
    public async Task TipoAusente_ConsultaDocumentosEAtoCorrigido()
    {
        Guid aviso = _cenario.AtoCorrigivel();
        PublicarAtoAvulsoDoProcessoCommand comando = _cenario.Comando(atoCorrigidoId: aviso, motivo: "Erro") with { TipoAtoCodigo = null };

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(comando);

        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("tipoAtoCodigo");
        await _cenario.Documentos.Received(1).ListarDoProcessoAsync(_cenario.ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _cenario.Atos.Received().ObterProcessoDoAtoAsync(aviso, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O assinante ausente sai junto da recusa do tipo, na ordem dos campos do pedido")]
    public async Task AssinanteAusenteEOutraRecusa_SaemJuntos()
    {
        _cenario.Tipos.ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando() with { Assinante = null });

        resposta.Errors.Select(e => e.Field).Should().Equal("assinante", "tipoAtoCodigo");
        resposta.Error!.Code.Should().Be(Obrigatorio);
    }

    [Fact(DisplayName = "O processo inexistente vem depois da forma e impede todas as consultas")]
    public async Task ProcessoInexistente_VemDepoisDaFormaEImpedeAsConsultas()
    {
        _cenario.Processos.ObterSiglaDaUnidadeAdministradoraAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns((string?)null);
        PublicarAtoAvulsoDoProcessoCommand comando = _cenario.Comando(atoCorrigidoId: Guid.CreateVersion7(), motivo: "Erro") with { Assinante = null };

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(comando);

        resposta.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(("assinante", Obrigatorio), (null, "ProcessoSeletivo.NaoEncontrado"));
        await NenhumaConsultaCorreu();
    }

    [Fact(DisplayName = "O processo sem página pública é recusado sem campo e impede todas as consultas")]
    public async Task ProcessoSemPaginaPublica_ImpedeAsConsultas()
    {
        _cenario.SemPaginaPublica();

        await _cenario.DeveRecusar(_cenario.Comando(atoCorrigidoId: Guid.CreateVersion7(), motivo: "Erro"), AtoDoProcessoErrorCodes.CertameNaoDivulgado);

        await NenhumaConsultaCorreu();
    }

    private const string FusoNaoReconhecido = "ProcessoSeletivo.FusoInstitucionalNaoReconhecido";

    [Fact(DisplayName = "O instante que falha é a única causa da resposta, sem campo, e nada é lido")]
    public async Task InstanteQueFalha_EAUnicaCausa()
    {
        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(), fuso: new ResolvedorFusoIndisponivelDeTeste());

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be(FusoNaoReconhecido);
        await NenhumaLeituraCorreu();
    }

    [Fact(DisplayName = "O instante que falha descarta as recusas de forma e nada é lido")]
    public async Task InstanteQueFalha_DescartaARecusaDeForma()
    {
        PublicarAtoAvulsoDoProcessoCommand comando = _cenario.Comando(atoCorrigidoId: Guid.CreateVersion7(), motivo: "Erro") with { Assinante = null, TipoAtoCodigo = null };

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(comando, fuso: new ResolvedorFusoIndisponivelDeTeste());

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be(FusoNaoReconhecido);
        await NenhumaLeituraCorreu();
    }

    [Fact(DisplayName = "O instante que falha vem antes do estado do processo: o processo inexistente não é lido nem acusado")]
    public async Task InstanteQueFalha_VemAntesDoProcessoInexistente()
    {
        _cenario.Processos.ObterSiglaDaUnidadeAdministradoraAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns((string?)null);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(), fuso: new ResolvedorFusoIndisponivelDeTeste());

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be(FusoNaoReconhecido);
        await NenhumaLeituraCorreu();
    }

    private async Task NenhumaLeituraCorreu()
    {
        await NenhumaConsultaCorreu();
        await _cenario.Processos.DidNotReceive().ObterSiglaDaUnidadeAdministradoraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cenario.Divulgados.DidNotReceive().ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private async Task NenhumaConsultaCorreu()
    {
        await _cenario.Tipos.DidNotReceive().ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _cenario.Atos.DidNotReceive().ObterProcessoDoAtoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
