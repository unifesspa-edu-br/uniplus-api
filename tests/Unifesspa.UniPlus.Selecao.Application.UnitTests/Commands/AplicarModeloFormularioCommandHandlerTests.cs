namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// A aplicação do modelo de formulário ao processo (UNI-REQ-0144, ADR-0061): confere o modelo,
/// descarta e relata o que o catálogo mudou, segue a regra do par inscrição e habilitação e copia do catálogo
/// as derivações que a cópia cita e o processo não tem.
/// </summary>
public sealed class AplicarModeloFormularioCommandHandlerTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    [Fact(DisplayName = "Página inválida do modelo gravado é recusada no caminho do modelo")]
    public async Task Handle_PaginaInvalidaDoModelo_RecusaNoCaminhoDasPaginas()
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Habilitacao, []);
        PaginaDoFormularioInput[] paginas =
        [
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).Construir(),
            EntradaDaPaginaDoFormularioDeTeste.Secao("OUTRA", 1).ComTitulo("").Construir(),
        ];
        modelo = modelo with { Conteudo = modelo.Conteudo with { Paginas = paginas } };

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(PapelDoFormularioDeTeste.Habilitacao, modelo);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("modelo.paginas[1].apresentacao.titulo", EstruturaFormularioErrorCodes.PaginaTituloInvalido));
    }

    [Fact(DisplayName = "Modelo inexistente é recusado no campo do modelo")]
    public async Task Handle_ModeloInexistente_Recusa()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Guid.NewGuid());

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(AplicacaoDeModeloErrorCodes.ModeloInexistente);
    }

    [Fact(DisplayName = "Modelo inativo e de outro tipo de processo é recusado pelos dois motivos")]
    public async Task Handle_ModeloInativoDeOutroTipo_RecusaOsDois()
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("QUILOMBOLA", 0)]) with { Ativo = false, TipoProcessoCodigo = "INDIGENA_E_QUILOMBOLA" };

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [AplicacaoDeModeloErrorCodes.ModeloInativo, AplicacaoDeModeloErrorCodes.TipoDeProcessoDiferente]);
    }

    [Fact(DisplayName = "Pressuposto que a inscrição do processo não coleta é recusado com o índice")]
    public async Task Handle_PressupostoAusente_Recusa()
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Habilitacao, [Item("CERTIFICADO", 0)], pressupostos: ["QUILOMBOLA"]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(PapelDoFormularioDeTeste.Habilitacao, modelo);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("modelo.pressupostos[0]");
    }

    [Theory(DisplayName = "O termo da cópia cita a faixa etária quando a cópia coleta a data de nascimento")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_TermoCitaFaixaEtaria_PelaDataDeNascimento(bool coletaDataDeNascimento)
    {
        Guid termoId = Guid.NewGuid();
        Guid versaoId = Guid.NewGuid();
        _cenario.Termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(
            [new VersaoTermoConsentimentoView(termoId, versaoId, "CONSULTA", 1, "Consulta", "Autorizo.", "Lei 12.711/2012", FormaAceite.RegistroComEnderecoIp, new string('b', 64))]);
        TermoExigidoInput termo = EntradaDoTermoExigidoDeTeste.Exigindo("CONSULTA").DaVersao(termoId, versaoId)
            .ExibidoQuando([[new CondicaoPrecondicaoInput("FAIXA_ETARIA", "MAIOR_IGUAL", JsonSerializer.SerializeToElement(18))]]).Construir();
        FatoColetadoInput[] itens = coletaDataDeNascimento ? [Item("QUILOMBOLA", 0), Item("DATA_NASCIMENTO", 1, "DATA")] : [Item("QUILOMBOLA", 0)];

        // Na habilitação, que não tem a seção do conjunto básico, a data de nascimento é coletada só se o modelo a pede.
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(
            PapelDoFormularioDeTeste.Habilitacao, Modelo(PapelDoFormularioDeTeste.Habilitacao, itens, termos: [termo]));

        resultado.IsSuccess.Should().Be(coletaDataDeNascimento, resultado.Error?.Message);
    }

    [Fact(DisplayName = "O grupo repetível do modelo é copiado para o formulário do processo")]
    public async Task Handle_ModeloComGrupo_CopiaOGrupo()
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("CERTIFICADO", 0)], grupos: [Composicao(Campo("MAIOR_IDADE", 0))]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _cenario.Processo.GruposColetados.Single().Subitens.Single().FatoCodigo.Should().Be("MAIOR_IDADE");
    }

    [Fact(DisplayName = "Grupo com campo de fato desativado sai da cópia inteiro e volta no relatório")]
    public async Task Handle_GrupoComCampoDesativado_DescartaOGrupo()
    {
        ModeloFormularioView modelo = Modelo(
            PapelDoFormularioDeTeste.Inscricao, [Item("CERTIFICADO", 0)], grupos: [Composicao(Campo("MAIOR_IDADE", 0), Campo("SOB_GUARDA", 1))]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.Value!.Descartados.Should().BeEquivalentTo([new ParteDescartadaDto("GRUPO", "COMPOSICAO_FAMILIAR", "FATO_DESATIVADO")]);
        _cenario.Processo.GruposColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O derivado citado só pelo campo do grupo recebe a regra padrão do catálogo")]
    public async Task Handle_DerivadoCitadoPeloCampoDoGrupo_CopiaARegra()
    {
        ModeloFormularioView modelo = Modelo(
            PapelDoFormularioDeTeste.Inscricao, [Item("QUILOMBOLA", 0)],
            grupos: [Composicao(ItemQueCita(Campo("MAIOR_IDADE", 0), "PERFIL", "IGUAL", "A"))]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.DerivacoesCopiadas.Should().Equal("PERFIL");
    }

    [Fact(DisplayName = "Item de fato desativado e termo de versão removida saem da cópia e voltam no relatório")]
    public async Task Handle_PartesQueOCatalogoMudou_DescartaERelata()
    {
        TermoExigidoInput termo = EntradaDoTermoExigidoDeTeste.Exigindo("LGPD").DaVersao(Guid.NewGuid(), Guid.NewGuid()).Construir();
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("BAIXA_RENDA", 0), Item("QUILOMBOLA", 1)], termos: [termo]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Descartados.Should().BeEquivalentTo(
            [new ParteDescartadaDto("ITEM", "BAIXA_RENDA", "FATO_DESATIVADO"), new ParteDescartadaDto("TERMO", "LGPD", "VERSAO_DE_TERMO_REMOVIDA")]);
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("QUILOMBOLA");
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O fato desativado que o processo já coleta não é vínculo novo e fica na cópia")]
    public async Task Handle_FatoDesativadoJaColetado_Mantem()
    {
        _cenario.Processo.DefinirItens([FatoBooleanoJaColetado("BAIXA_RENDA", "Baixa renda")]).IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("BAIXA_RENDA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Descartados.Should().BeEmpty();
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("BAIXA_RENDA");
    }

    [Fact(DisplayName = "O que o item descartado cita não pede derivação nem distribuição de vagas")]
    public async Task Handle_ItemDescartadoQueCitaDerivado_AplicaSemExigirDerivacao()
    {
        FatoColetadoInput descartado = ItemQueCita(Item("BAIXA_RENDA", 1), "SEM_REGRA", "ALGUM_DE", new[] { "X" });

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("QUILOMBOLA", 0), descartado]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Descartados.Should().ContainSingle().Which.Codigo.Should().Be("BAIXA_RENDA");
    }

    [Fact(DisplayName = "A recusa de um item aponta a posição dele no modelo, mesmo depois de um descarte")]
    public async Task Handle_RecusaDepoisDeDescarte_ApontaAPosicaoNoModelo()
    {
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("BAIXA_RENDA", 0), Montar("QUILOMBOLA", 1).ComRotulo("").Construir()]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("modelo.itens[1].apresentacao.rotulo");
    }

    [Fact(DisplayName = "A recusa do estado final aponta o termo pela posição no modelo, depois de um descarte")]
    public async Task Handle_RecusaDoAgregadoDepoisDeDescarte_ApontaAPosicaoNoModelo()
    {
        _cenario.Processo.DefinirItens([FatoBooleanoJaColetado("CERTIFICADO", "Certificado")], papel: PapelDoFormularioDeTeste.IsencaoTaxa).IsSuccess.Should().BeTrue();
        Guid termoId = Guid.NewGuid();
        Guid versaoId = Guid.NewGuid();
        _cenario.Termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new VersaoTermoConsentimentoView(termoId, versaoId, "TERMO", 1, "Termo", "Texto", "Base", FormaAceite.RegistroSemEnderecoIp, "hash")]);
        TermoExigidoInput removido = EntradaDoTermoExigidoDeTeste.Exigindo("LGPD").DaVersao(Guid.NewGuid(), Guid.NewGuid()).Construir();
        TermoExigidoInput citaIsencao = EntradaDoTermoExigidoDeTeste.Exigindo("IMAGEM", 1).DaVersao(termoId, versaoId)
            .ExibidoQuando([[new CondicaoPrecondicaoInput("CERTIFICADO", "IGUAL", JsonSerializer.SerializeToElement(true))]]).Construir();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(
            PapelDoFormularioDeTeste.Habilitacao, Modelo(PapelDoFormularioDeTeste.Habilitacao, [Item("QUILOMBOLA", 0)], termos: [removido, citaIsencao]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("modelo.termos[1]");
    }

    [Fact(DisplayName = "Processo inexistente é recusado como não encontrado, sem consultar o modelo nem gravar")]
    public async Task Handle_ProcessoInexistente_RecusaSemConsultarOModelo()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAoProcessoInexistenteAsync(Guid.NewGuid());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AcessoAoProcesso.NaoEncontrado().Code);
        await _cenario.Modelos.DidNotReceiveWithAnyArgs().ObterAsync(default, default);
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo já publicado é recusado pela situação, sem consultar o modelo nem gravar")]
    public async Task Handle_ProcessoPublicado_RecusaSemConsultarOModelo()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAoProcessoPublicadoAsync(Guid.NewGuid());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await _cenario.Modelos.DidNotReceiveWithAnyArgs().ObterAsync(default, default);
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
