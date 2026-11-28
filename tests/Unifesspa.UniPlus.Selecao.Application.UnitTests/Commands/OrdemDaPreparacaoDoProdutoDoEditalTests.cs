namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaPublicacaoDoProdutoDoEdital;

/// <summary>
/// A ordem única das recusas da publicação de um item do edital: o instante, que sozinho encerra a resposta quando
/// falha; a forma do pedido, o estado do processo, o item, os documentos, a política de divulgação e o ato corrigido, e o
/// que uma recusa impede de consultar.
/// </summary>
public sealed class OrdemDaPreparacaoDoProdutoDoEditalTests
{
    private const string Obrigatorio = "uniplus.validacao.obrigatorio";

    private readonly CenarioDaPublicacaoDoProdutoDoEdital _cenario = new();

    [Fact(DisplayName = "O item não declarado e o documento inexistente saem juntos, e o tipo do item não é consultado")]
    public async Task ItemNaoDeclaradoEDocumentoAusente_SaemJuntosSemConsultarOTipo()
    {
        _cenario.ParNaMesmaFase();

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(
            _cenario.Comando(Guid.CreateVersion7()) with { DocumentoPrincipalId = Guid.CreateVersion7() });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(
            (null, AtoDoProcessoErrorCodes.ProdutoNaoDeclarado),
            ("documentoPrincipalId", AtoDoProcessoErrorCodes.DocumentoNaoEncontrado));
        await _cenario.Tipos.DidNotReceive().ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo sem versão vigente é recusado sem campo, e a política não é julgada, porque lê o tipo")]
    public async Task TipoSemVersaoVigente_NaoJulgaAPolitica()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Tipos.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id, politica: false));

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().BeNull();
        recusa.Error.Code.Should().Be("ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
    }

    [Fact(DisplayName = "O tipo que congela configuração é recusado sem campo, e a política não é julgada")]
    public async Task TipoQueCongela_NaoJulgaAPolitica()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Congelado = _cenario.Congelado with { CongelaConfiguracao = true };

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id, politica: false));

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(AtoDoProcessoErrorCodes.TipoCongelaConfiguracao);
    }

    [Fact(DisplayName = "O item já publicado e o documento inexistente saem juntos: a primeira publicação não depende do documento")]
    public async Task ItemJaPublicadoEDocumentoAusente_SaemJuntos()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Publicado(Materia, preliminar.Id);

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id) with { DocumentoPrincipalId = Guid.CreateVersion7() });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(
            (null, AtoDoProcessoErrorCodes.ProdutoJaPublicado),
            ("documentoPrincipalId", AtoDoProcessoErrorCodes.DocumentoNaoEncontrado));
    }

    [Fact(DisplayName = "O tipo recusado e o item já publicado saem juntos: a primeira publicação não depende do tipo")]
    public async Task TipoRecusadoEItemJaPublicado_SaemJuntos()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Publicado(Materia, preliminar.Id);
        _cenario.Congelado = _cenario.Congelado with { CongelaConfiguracao = true };

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id));

        recusas.Select(r => r.Error.Code).Should().Equal(AtoDoProcessoErrorCodes.TipoCongelaConfiguracao, AtoDoProcessoErrorCodes.ProdutoJaPublicado);
    }

    [Fact(DisplayName = "O documento inexistente vem antes da política não confirmada, que aponta o campo da confirmação")]
    public async Task DocumentoAusenteEPoliticaNaoConfirmada_SaemNaOrdemDoPedido()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(
            _cenario.Comando(preliminar.Id, politica: false) with { DocumentoPrincipalId = Guid.CreateVersion7() });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(
            ("documentoPrincipalId", AtoDoProcessoErrorCodes.DocumentoNaoEncontrado),
            ("politicaDeDivulgacaoConfirmada", AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada));
    }

    [Fact(DisplayName = "O usuário não identificado é recusado sem campo")]
    public async Task UsuarioNaoIdentificado_RecusaSemCampo()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Usuario.UserId.Returns((string?)null);

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id));

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().BeNull();
        recusa.Error.Code.Should().Be(AtoDoProcessoErrorCodes.UsuarioNaoIdentificado);
    }

    [Theory(DisplayName = "O assinante ausente, nulo, vazio ou em branco é recusado no campo dele como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssinanteAusente_RecusaNoCampoDele(string? assinante)
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id) with { Assinante = assinante });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(("assinante", Obrigatorio));
    }

    [Fact(DisplayName = "O assinante ausente sai junto do item não declarado, forma primeiro")]
    public async Task AssinanteAusenteEItemNaoDeclarado_SaemJuntos()
    {
        _cenario.ParNaMesmaFase();

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(Guid.CreateVersion7()) with { Assinante = null });

        recusas.Select(r => r.Error.Code).Should().Equal(Obrigatorio, AtoDoProcessoErrorCodes.ProdutoNaoDeclarado);
    }

    [Fact(DisplayName = "Mais de 20 anexos é recusado no campo dos anexos: os documentos não são lidos, mas o item continua conferido")]
    public async Task AnexosAlemDoLimite_NaoLeDocumentosEConferePorItem()
    {
        _cenario.ParNaMesmaFase();
        AnexoSolicitadoDoAto[] anexos = [.. Enumerable.Range(0, 21).Select(i => new AnexoSolicitadoDoAto(Guid.CreateVersion7(), $"anexo {i}"))];

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(Guid.CreateVersion7()) with { Anexos = anexos });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(
            ("anexos", AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida),
            (null, AtoDoProcessoErrorCodes.ProdutoNaoDeclarado));
        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A correção de item não declarado confere o ato corrigido, mas não confere se ele é do item")]
    public async Task CorrecaoDeItemNaoDeclarado_NaoConfereSeOAtoEDoItem()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        AtoDoProcesso original = _cenario.Publicado(Materia, preliminar.Id);

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(Guid.CreateVersion7(), corrige: original.Id));

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(AtoDoProcessoErrorCodes.ProdutoNaoDeclarado);
        await _cenario.Atos.Received().ObterProcessoDoAtoAsync(original.Id, Arg.Any<CancellationToken>());
        await _cenario.Atos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O ato corrigido recusado aponta o campo dele, e não se confere se é do item nem se o preliminar está fechado")]
    public async Task AtoCorrigidoRecusado_NaoConfereOItem()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id, corrige: Guid.CreateVersion7()));

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(("atoCorrigidoId", AtoDoProcessoErrorCodes.AtoCorrigidoNaoEncontrado));
        await _cenario.Atos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O ato que não é do item aponta o campo do ato corrigido")]
    public async Task AtoDeOutroItem_ApontaOCampoDoAtoCorrigido()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = _cenario.ParNaMesmaFase();
        AtoDoProcesso doPreliminar = _cenario.Publicado(Materia, preliminar.Id);

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(definitivo.Id, corrige: doPreliminar.Id));

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(("atoCorrigidoId", AtoDoProcessoErrorCodes.AtoCorrigidoNaoEhDoProduto));
    }

    [Fact(DisplayName = "O motivo sem ato corrigido dá só a causa do ato corrigido obrigatório: a primeira publicação não é julgada")]
    public async Task MotivoSemAtoCorrigido_NaoEscolheEntrePrimeiraPublicacaoECorrecao()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Publicado(Materia, preliminar.Id);

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id) with { MotivoDaCorrecao = "Erro" });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(("atoCorrigidoId", AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio));
    }

    [Fact(DisplayName = "O processo inexistente vem depois da forma e impede todas as consultas")]
    public async Task ProcessoInexistente_VemDepoisDaFormaEImpedeAsConsultas()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Processos.ObterSiglaDaUnidadeAdministradoraAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns((string?)null);

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(preliminar.Id, corrige: Guid.CreateVersion7()) with { Assinante = null });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(("assinante", Obrigatorio), (null, "ProcessoSeletivo.NaoEncontrado"));
        await _cenario.Processos.DidNotReceive().ObterVersaoVigenteAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _cenario.Atos.DidNotReceive().ObterProcessoDoAtoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O documento principal ausente vem antes do item não declarado, e os documentos não são lidos")]
    public async Task DocumentoPrincipalAusenteEItemNaoDeclarado_FormaPrimeiro()
    {
        _cenario.ParNaMesmaFase();

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(_cenario.Comando(Guid.CreateVersion7()) with { DocumentoPrincipalId = null });

        recusas.Select(r => (r.Field, r.Error.Code)).Should().Equal(
            ("documentoPrincipalId", Obrigatorio), (null, AtoDoProcessoErrorCodes.ProdutoNaoDeclarado));
        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A forma sai na ordem do pedido: data, assinante, documento principal, anexo e, por fim, a correção")]
    public async Task FormaRecusada_SaiNaOrdemDoPedido()
    {
        PublicarProdutoDoEditalCommand comando = _cenario.Comando(Guid.CreateVersion7(), corrige: Guid.CreateVersion7()) with
        {
            MotivoDaCorrecao = null,
            DataDoAto = null,
            Assinante = null,
            DocumentoPrincipalId = null,
            Anexos = [new(null, "A")],
        };

        IReadOnlyList<FieldError> recusas = await _cenario.Recusas(comando);

        recusas.Take(5).Select(r => r.Field).Should().Equal(
            "dataDoAto", "assinante", "documentoPrincipalId", "anexos[0].documentoId", "motivoDaCorrecao");
        recusas.Take(4).Should().OnlyContain(r => r.Error.Code == Obrigatorio);
    }

    [Fact(DisplayName = "O instante que falha é a única causa da resposta, sem campo, e nada é lido")]
    public async Task InstanteQueFalha_EAUnicaCausaEDescartaAForma()
    {
        _cenario.ParNaMesmaFase();
        PublicarProdutoDoEditalCommand comando = _cenario.Comando(Guid.CreateVersion7()) with { Assinante = null, DataDoAto = null };

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(comando, fuso: new ResolvedorFusoIndisponivelDeTeste());

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FusoInstitucionalNaoReconhecido");
        await _cenario.Processos.DidNotReceive().ObterSiglaDaUnidadeAdministradoraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cenario.Processos.DidNotReceive().ObterVersaoVigenteAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo é travado antes de qualquer leitura e o ato é salvo uma vez")]
    public async Task ProdutoPublicado_TravaAntesDasLeiturasESalvaUmaVez()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        TravamentoDoProcessoDeTeste travamento = TravamentoDoProcessoDeTeste.Observar(
            _cenario.Processos, _cenario.Divulgados, _cenario.Atos, _cenario.Documentos, _cenario.Tipos, _cenario.Codecs, _cenario.Registro);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(preliminar.Id));

        resposta.IsSuccess.Should().BeTrue();
        travamento.Travou.Should().BeTrue();
        travamento.LeiturasAntes.Should().BeEmpty("nenhuma fonte (processo, divulgação, atos, documentos ou catálogo de tipos) é lida antes do travamento");
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A forma recusada também é conferida depois de travar o processo")]
    public async Task FormaRecusada_TravaOProcessoAntes()
    {
        _cenario.ParNaMesmaFase();

        await _cenario.Recusas(_cenario.Comando(Guid.CreateVersion7(), corrige: Guid.CreateVersion7()) with { MotivoDaCorrecao = null });

        await _cenario.Processos.Received(1).TravarAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
    }
}
