namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaPublicacaoDoAtoAvulso;

/// <summary>
/// A gravação do ato avulso aceito: o registro do ato do processo e a entrega à porta do registro central na mesma
/// transação, o instante lido uma vez e as recusas que só a gravação e a porta enxergam.
/// </summary>
public sealed class PublicarAtoAvulsoDoProcessoCommandHandlerTests
{
    private readonly CenarioDaPublicacaoDoAtoAvulso _cenario = new();

    [Fact(DisplayName = "O aviso com um anexo grava o registro do ato e o entrega à porta com o hash do principal e a lista de anexos, antes de salvar")]
    public async Task Aviso_ComAnexo_GravaORegistroEEntregaAPorta()
    {
        AtoDoProcesso? gravado = null;
        await _cenario.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());
        AtoNormativoPublicado? entregue = null;
        _cenario.Registro.RegistrarAsync(Arg.Do<AtoNormativoPublicado>(a => entregue = a), Arg.Any<CancellationToken>()).Returns(Result.Success());

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) =
            await _cenario.Publicar(_cenario.Comando(anexos: [new AnexoSolicitadoDoAto(_cenario.Anexo.Id, "Quadro de vagas")]));

        resposta.IsSuccess.Should().BeTrue();
        gravado.Should().NotBeNull();
        resposta.Value!.AtoId.Should().Be(gravado!.Id);
        ConferirRegistro(gravado, mensagens);
        ExigirOrdemDaGravacao();
        ConferirEntrega(entregue!, gravado.Id);
    }

    private void ConferirRegistro(AtoDoProcesso gravado, IEnumerable<object> mensagens)
    {
        gravado.IdentificadorNoEnderecoWebDaUnidadeAdministradora.Should().Be("ceps", "a chave no acervo usa a unidade e o identificador da versão divulgada");
        gravado.IdentificadorLegivelDoProcesso.Should().Be("psm-medicina-2027");
        gravado.DocumentoPrincipalId.Should().Be(_cenario.Principal.Id);
        gravado.Anexos.Should().ContainSingle().Which.NomeDeExibicao.Should().Be("Quadro de vagas.docx");
        mensagens.Should().ContainSingle().Which.Should().Be(new CopiarDocumentosDoAtoAoAcervo(_cenario.ProcessoId, gravado.Id));
    }

    private void ExigirOrdemDaGravacao() =>
        Received.InOrder(() =>
        {
            _ = _cenario.Atos.AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
            _ = _cenario.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>());
            _ = _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });

    private void ConferirEntrega(AtoNormativoPublicado entregue, Guid atoId)
    {
        entregue.AtoId.Should().Be(atoId);
        entregue.Orgao.Should().Be("CEPS");
        entregue.Serie.Should().Be("AVISO");
        entregue.Ano.Should().Be(2026);
        entregue.DocumentoHash.Should().Be(_cenario.Principal.HashSha256);
        entregue.VersaoDoObjeto.Should().BeNull("o ato avulso não congela configuração");
        entregue.Retificacao.Should().BeNull();
        entregue.Vinculos.Should().ContainSingle().Which.EntidadeId.Should().Be(_cenario.ProcessoId);
        entregue.Anexos.Should().ContainSingle().Which.Should().Be(
            new AnexoDoAtoPublicado("Quadro de vagas.docx", TiposDeMidiaDeDocumento.Docx, _cenario.Anexo.HashSha256!));
    }

    [Fact(DisplayName = "O ato avulso lê o relógio uma vez e usa o instante no registro gravado e no ato entregue à porta")]
    public async Task Avulso_LeORelogioUmaVez_EOInstanteVaiParaORegistroEParaAPorta()
    {
        RelogioDeLeituraContada relogio = new(Agora);
        AtoDoProcesso? gravado = null;
        await _cenario.Atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(), relogio);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        relogio.Leituras.Should().Be(1);
        gravado!.PublicadoEm.Should().Be(Agora);
        await _cenario.Registro.Received(1).RegistrarAsync(Arg.Is<AtoNormativoPublicado>(a => a.PublicadoEm == Agora), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo do ato avulso é consultado no dia do instante, e a data que o documento declara não entra na consulta")]
    public async Task Avulso_ConsultaOTipoNoDiaDoInstante_NaoNoDaDataDoAto()
    {
        DateOnly declarada = DataDoAto.AddDays(-3);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando() with { DataDoAto = declarada });

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await _cenario.Tipos.Received().ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>());
        await _cenario.Tipos.DidNotReceive().ObterVigenteAsync("AVISO", declarada, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo é travado antes de qualquer leitura, até a da forma do pedido recusada")]
    public async Task Avulso_TravaOProcessoAntesDeQualquerLeitura()
    {
        TravamentoDoProcessoDeTeste travamento = TravamentoDoProcessoDeTeste.Observar(
            _cenario.Processos, _cenario.Divulgados, _cenario.Atos, _cenario.Documentos, _cenario.Tipos, _cenario.Registro);

        await _cenario.Publicar(_cenario.Comando() with { Assinante = null });

        travamento.Travou.Should().BeTrue();
        travamento.LeiturasAntes.Should().BeEmpty("nenhuma fonte (processo, divulgação, atos, documentos ou catálogo de tipos) é lida antes do travamento");
        await _cenario.Processos.Received().ObterSiglaDaUnidadeAdministradoraAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A errata que aponta um aviso publicado é aceita com o motivo, e a porta recebe o ato corrigido")]
    public async Task Errata_ApontandoAvisoPublicado_EAceita()
    {
        Guid avisoId = _cenario.AtoCorrigivel();

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(atoCorrigidoId: avisoId, motivo: "Data errada no cronograma"));

        resposta.IsSuccess.Should().BeTrue();
        await _cenario.Registro.Received(1).RegistrarAsync(
            Arg.Is<AtoNormativoPublicado>(a => a.Retificacao == new RetificacaoDoAto(avisoId, "Data errada no cronograma")), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Aviso e comunicado, como o seed os declara, são aceitos sem ato corrigido")]
    [InlineData("AVISO")]
    [InlineData("COMUNICADO")]
    public async Task AvisoEComunicado_SemAtoCorrigido_SaoAceitos(string codigo)
    {
        _cenario.Tipos.ObterVigenteAsync(codigo, DataDoAto, Arg.Any<CancellationToken>()).Returns(SeedDosTiposDeAto.Tipo(codigo));

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(tipo: codigo));

        resposta.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A recusa da porta, como a vaga do objeto ocupada, devolve a recusa nomeada, descarta o rastreado e não grava")]
    public async Task RecusaDaPorta_DevolveADescartaENaoGrava()
    {
        DomainError vagaOcupada = new("AtoNormativo.ObjetoJaTemAtoVivoDoTipo", "O objeto já tem o ato AVISO nº 1/2026 vivo.");
        _cenario.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(vagaOcupada));

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Publicar(_cenario.Comando());

        resposta.Error.Should().Be(vagaOcupada);
        mensagens.Should().BeEmpty();
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O conflito que só a gravação enxerga e a porta reconhece devolve a recusa da porta e descarta o rastreado")]
    public async Task ConflitoDaGravacaoReconhecidoPelaPorta_DevolveARecusaDaPorta()
    {
        DomainError atoJaRetificado = new("AtoNormativo.AtoJaRetificado", "O ato AVISO nº 1/2026 já foi retificado.");
        Exception violacao = ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_atos_normativos_retificado");
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).Returns<Task>(_ => throw violacao);
        _cenario.Registro.TraduzirConflitoDaGravacao(violacao).Returns(atoJaRetificado);

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Publicar(_cenario.Comando());

        resposta.Error.Should().Be(atoJaRetificado);
        mensagens.Should().BeEmpty();
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "O conflito da correção única é da Seleção e vem antes da porta, que nem é consultada")]
    public async Task ConflitoDaCorrecaoUnica_EDaSelecaoENaoConsultaAPorta()
    {
        Guid aviso = _cenario.AtoCorrigivel();
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_atos_do_processo_ato_corrigido"));

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(atoCorrigidoId: aviso, motivo: "Erro"));

        resposta.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido);
        _cenario.Registro.DidNotReceive().TraduzirConflitoDaGravacao(Arg.Any<Exception>());
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A exceção que nem a Seleção nem a porta reconhecem propaga")]
    public async Task ExcecaoNaoReconhecida_Propaga()
    {
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new InvalidOperationException("Conexão perdida."));

        Func<Task> publicar = () => _cenario.Publicar(_cenario.Comando());

        await publicar.Should().ThrowAsync<InvalidOperationException>();
        _cenario.Registro.Received(1).TraduzirConflitoDaGravacao(Arg.Any<InvalidOperationException>());
    }
}
