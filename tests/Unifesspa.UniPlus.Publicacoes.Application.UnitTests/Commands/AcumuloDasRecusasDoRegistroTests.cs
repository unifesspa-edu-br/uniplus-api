namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O registro do ato devolve todas as recusas numa só resposta, campo a campo na ordem do corpo do pedido,
/// com a leitura do banco só para a parte que a forma aceitou.
/// </summary>
public sealed class AcumuloDasRecusasDoRegistroTests
{
    private const string Hash = ComandoDeRegistroDeTeste.HashValido;
    private static readonly Guid Processo = Guid.CreateVersion7();

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "Órgão em branco, série longa demais e hash inválido são recusados juntos, na ordem do corpo")]
    public async Task Montar_TresRecusasDeForma_DevolveAsTresNaOrdemDoCorpo()
    {
        _tipos.ComTipoVigente();

        Result<AtoMontado> montado = await Montar(
            Comando() with { Orgao = "   ", Serie = new string('s', 101), DocumentoHash = "abc" });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("serie", AtoNormativoErrorCodes.TextoInvalido),
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido));
        montado.Error!.Code.Should().Be(AtoNormativoErrorCodes.TextoInvalido);
        await ConsultouOCatalogoUmaVez();
    }

    [Fact(DisplayName = "Órgão em branco e ato já retificado são recusados juntos, com o código da primeira recusa na raiz")]
    public async Task Montar_OrgaoEmBrancoEAtoJaRetificado_DevolveOsDois()
    {
        _tipos.ComTipoVigente();
        AtoNormativo retificado = AtoExistente();
        _atos.ComoRetificavel(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(AtoExistente());

        Result<AtoMontado> montado = await Montar(Retificando(retificado.Id) with { Orgao = "" });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("atoRetificadoId", AtoNormativoErrorCodes.AtoJaRetificado));
        montado.Error!.Code.Should().Be(AtoNormativoErrorCodes.TextoInvalido);
    }

    [Fact(DisplayName = "Ato retificado vazio e hash inválido: a cadeia não é lida e o catálogo é")]
    public async Task Montar_AtoRetificadoVazioEHashInvalido_NaoLeACadeia()
    {
        _tipos.ComTipoVigente();

        Result<AtoMontado> montado = await Montar(Retificando(Guid.Empty) with { DocumentoHash = "abc" });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido),
            ("atoRetificadoId", AtoNormativoErrorCodes.RetificacaoIncompleta));
        await _atos.DidNotReceiveWithAnyArgs().ObterPorIdParaLeituraAsync(default, default);
        await ConsultouOCatalogoUmaVez();
    }

    [Fact(DisplayName = "Tipo sem versão vigente, ato retificado inexistente e vínculo proibido saem na ordem do corpo")]
    public async Task Montar_TipoSemVersaoAtoInexistenteEVinculoProibido_DevolveOsTresNaOrdem()
    {
        Guid inexistente = Guid.CreateVersion7();

        Result<AtoMontado> montado = await Montar(
            Retificando(inexistente) with { Vinculos = [new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Processo)] });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("tipoCodigo", AtoNormativoErrorCodes.TipoSemVersaoVigente),
            ("atoRetificadoId", AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado),
            ("vinculos[0].entidadeTipo", AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio));
        await _atos.DidNotReceiveWithAnyArgs().ListarVinculosDoAtoAsync(default, default);
    }

    [Fact(DisplayName = "As mensagens não repetem o código do tipo, o tipo da entidade nem o ato retificado recebidos")]
    public async Task Montar_Recusas_NaoRepetemValorRecebido()
    {
        Guid inexistente = Guid.CreateVersion7();

        Result<AtoMontado> montado = await Montar(
            Retificando(inexistente) with { Vinculos = [new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Processo)] });

        string[] mensagens = [.. montado.Errors.Select(recusa => recusa.Error.Message)];
        mensagens.Should().HaveCount(3);
        mensagens.Should().NotContain(mensagem => mensagem.Contains(ComandoDeRegistroDeTeste.Tipo, StringComparison.Ordinal)
            || mensagem.Contains(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, StringComparison.Ordinal)
            || mensagem.Contains(inexistente.ToString(), StringComparison.Ordinal));
        mensagens[0].Should().Contain("(2026-03-13)");
    }

    [Fact(DisplayName = "Vínculo proibido com identificador vazio recusa o tipo e o identificador, nessa ordem")]
    public async Task Montar_VinculoProibidoSemIdentificador_RecusaTipoEIdentificador()
    {
        _tipos.ComTipoVigente();

        Result<AtoMontado> montado = await Montar(Comando() with
        {
            Vinculos = [new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Guid.Empty)],
        });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("vinculos[0].entidadeTipo", AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio),
            ("vinculos[0].entidadeId", AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade));
    }

    [Fact(DisplayName = "Vínculo herdado que só o domínio dono registra é recusado no ato retificado, junto com o órgão em branco")]
    public async Task Montar_VinculoHerdadoProibido_RecusaNoAtoRetificado()
    {
        _tipos.ComTipoVigente();
        AtoNormativo retificado = AtoExistente();
        _atos.ComoRetificavel(retificado);
        _atos.ListarVinculosDoAtoAsync(retificado.Id, Arg.Any<CancellationToken>())
            .Returns([new EntidadeVinculada(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Processo)]);

        Result<AtoMontado> montado = await Montar(Retificando(retificado.Id) with { Orgao = "" });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("atoRetificadoId", AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio));
    }

    [Fact(DisplayName = "Elemento nulo na lista de vínculos chega ao domínio e é recusado na posição")]
    public async Task Montar_VinculoNulo_RecusaNaPosicao()
    {
        _tipos.ComTipoVigente();

        Result<AtoMontado> montado = await Montar(Comando() with
        {
            Vinculos = [new("OBJETO_A", Processo), null!],
        });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(("vinculos[1]", AtoNormativoErrorCodes.VinculoNulo));
    }

    [Fact(DisplayName = "Os cinco textos obrigatórios omitidos são recusados pelo domínio, sem ler o catálogo")]
    public async Task Montar_TextosObrigatoriosNulos_RecusaPeloDominio()
    {
        Result<AtoMontado> montado = await Montar(
            Comando() with { Orgao = null, Serie = null, TipoCodigo = null, DocumentoHash = null, Assinante = null });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("serie", AtoNormativoErrorCodes.TextoInvalido),
            ("tipoCodigo", TipoAtoPublicadoErrorCodes.CodigoObrigatorio),
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido),
            ("assinante", AtoNormativoErrorCodes.TextoInvalido));
        await _tipos.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }

    [Fact(DisplayName = "Data do ato omitida é recusada como obrigatória, entre o órgão e o hash, e o catálogo ainda é lido")]
    public async Task Montar_DataDoAtoOmitida_RecusaComoObrigatoriaNaOrdemDoCorpo()
    {
        _tipos.ComTipoVigente();

        Result<AtoMontado> montado = await Montar(Comando() with { Orgao = "", DataDoAto = null, DocumentoHash = "abc" });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("orgao", AtoNormativoErrorCodes.TextoInvalido),
            ("dataDoAto", CodigosDeValidacao.Obrigatorio),
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido));
        await ConsultouOCatalogoUmaVez();
    }

    [Fact(DisplayName = "Data do ato omitida não grava o ano 1: nada é montado nem reservado")]
    public async Task Montar_DataDoAtoOmitida_NaoMontaNemReservaVaga()
    {
        _tipos.ComTipoVigente(unicoPorObjeto: true);

        Result<AtoMontado> montado = await Montar(Comando() with { DataDoAto = null, Vinculos = [new("OBJETO_A", Processo)] });

        montado.IsFailure.Should().BeTrue();
        montado.Errors.Should().ContainSingle(e => e.Field == "dataDoAto");
        await _atos.DidNotReceiveWithAnyArgs().ReservarVagaAsync(default!, default);
    }

    [Fact(DisplayName = "Tipo da entidade vinculada e os três campos do anexo omitidos são recusados pelo domínio")]
    public async Task Montar_CamposAninhadosNulos_RecusaPeloDominio()
    {
        _tipos.ComTipoVigente();

        Result<AtoMontado> montado = await Montar(
            Comando() with { Vinculos = [new(null, Processo)], Anexos = [new(null, null, null)] });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(
            ("vinculos[0].entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido),
            ("anexos[0].nome", AnexoDoAtoErrorCodes.NomeInvalido),
            ("anexos[0].tipoMidia", AnexoDoAtoErrorCodes.TipoMidiaInvalido),
            ("anexos[0].hash", AnexoDoAtoErrorCodes.HashInvalido));
    }

    [Fact(DisplayName = "A vaga do objeto só é consultada e reservada quando não há nenhuma recusa")]
    public async Task Montar_ComRecusa_NaoReservaVaga()
    {
        _tipos.ComTipoVigente(unicoPorObjeto: true);

        Result<AtoMontado> montado = await Montar(Comando() with
        {
            Orgao = "",
            Vinculos = [new("OBJETO_A", Processo)],
        });

        ResumoDasRecusas.De(montado.Errors).Should().Equal(("orgao", AtoNormativoErrorCodes.TextoInvalido));
        await _atos.DidNotReceiveWithAnyArgs().ObterAtoConflitanteNoObjetoAsync(default, default!, default!, default);
        await _atos.DidNotReceiveWithAnyArgs().ReservarVagaAsync(default!, default);
    }

    private async Task ConsultouOCatalogoUmaVez() =>
        await _tipos.Received(1).ObterVigenteAsync(
            ComandoDeRegistroDeTeste.Tipo, ComandoDeRegistroDeTeste.DiaDaPublicacao, Arg.Any<CancellationToken>());

    private Task<Result<AtoMontado>> Montar(RegistrarAtoNormativoCommand comando) =>
        MontagemDeTeste.Criar(_tipos, _atos)
            .MontarAsync(comando, ComandoDeRegistroDeTeste.PublicadoEm, CancellationToken.None);

    private static RegistrarAtoNormativoCommand Retificando(Guid atoRetificadoId) =>
        Comando() with { AtoRetificadoId = atoRetificadoId, MotivoRetificacao = "corrige o anexo" };

    private static RegistrarAtoNormativoCommand Comando() => ComandoDeRegistroDeTeste.Valido();

    private static AtoNormativo AtoExistente() =>
        new AtoNormativoDeTeste
        {
            TipoCodigo = ComandoDeRegistroDeTeste.Tipo,
            DocumentoHash = Hash,
            PublicadoEm = ComandoDeRegistroDeTeste.PublicadoEm,
        }.Registrar();
}
