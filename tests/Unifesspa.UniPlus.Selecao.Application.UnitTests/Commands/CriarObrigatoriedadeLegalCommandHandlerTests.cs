namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A existência e a atividade do código de uma regra legal pertencem à Configuração;
/// a factory de domínio apenas preserva o código que o handler validou.
/// </summary>
public sealed class CriarObrigatoriedadeLegalCommandHandlerTests
{
    private const string TipoEtapaCodigo = "PROVA_OBJETIVA";

    private static ITipoEtapaReader TipoEtapaReaderAtivo() =>
        TipoEtapaReaderComResposta(new TipoEtapaView(Guid.CreateVersion7(), TipoEtapaCodigo, "Prova Objetiva", null));

    /// <summary>Leitor que dá por viva qualquer modalidade consultada — o caso comum dos testes de outro assunto.</summary>
    private static IModalidadeReader ModalidadeReaderViva()
    {
        IModalidadeReader reader = Substitute.For<IModalidadeReader>();
        reader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ModalidadeViva(call.Arg<string>()));
        return reader;
    }

    /// <summary>Leitor que dá por vivo qualquer tipo de documento consultado.</summary>
    private static ITipoDocumentoReader TipoDocumentoReaderVivo()
    {
        ITipoDocumentoReader reader = Substitute.For<ITipoDocumentoReader>();
        reader.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => TipoDocumentoVivo(call.Arg<string>()));
        return reader;
    }

    private static IModalidadeReader ModalidadeReaderSem(string codigoAusente)
    {
        IModalidadeReader reader = Substitute.For<IModalidadeReader>();
        reader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>() == codigoAusente ? null : ModalidadeViva(call.Arg<string>()));
        return reader;
    }

    private static ITipoDocumentoReader TipoDocumentoReaderSem(string codigoAusente)
    {
        ITipoDocumentoReader reader = Substitute.For<ITipoDocumentoReader>();
        reader.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>() == codigoAusente ? null : TipoDocumentoVivo(call.Arg<string>()));
        return reader;
    }

    private static ModalidadeView ModalidadeViva(string codigo) =>
        new(Guid.CreateVersion7(), codigo, null, "COTA_RESERVADA", "DENTRO_DO_VR", null, null, null, null, null, [], null, null);

    private static TipoDocumentoView TipoDocumentoVivo(string codigo) =>
        new(Guid.CreateVersion7(), codigo, "Documento", "OUTROS");

    private static ITipoEtapaReader TipoEtapaReaderComResposta(TipoEtapaView? resposta)
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorCodigoAsync(TipoEtapaCodigo, Arg.Any<CancellationToken>()).Returns(resposta);
        return reader;
    }

    [Fact(DisplayName = "Handle aceita código de tipo ativo e persiste a obrigatoriedade")]
    public async Task Handle_TipoAtivo_Persiste()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_NOVO"), repository, tipoReader, tipoEtapaReader, ModalidadeReaderViva(), TipoDocumentoReaderVivo(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ObrigatoriedadeLegal>(regra => regra.TipoProcessoCodigo == "PS_NOVO"),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa código de tipo de processo inexistente ou desativado sem persistir regra")]
    public async Task Handle_TipoProcessoInexistenteOuInativo_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_DESATIVADO", Arg.Any<CancellationToken>())
            .Returns((TipoProcessoView?)null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_DESATIVADO"), repository, tipoReader, tipoEtapaReader, ModalidadeReaderViva(), TipoDocumentoReaderVivo(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle aceita o sentinela universal sem consultar os tipos de processo ativos")]
    public async Task Handle_TipoUniversal_NaoConsultaTiposAtivos()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra(ObrigatoriedadeLegal.TipoProcessoUniversal), repository, tipoReader, tipoEtapaReader, ModalidadeReaderViva(), TipoDocumentoReaderVivo(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipoReader.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    /// <summary>issue #1071 — decisão fechada: obrigatoriedade nova só pode usar código de tipo de etapa ativo.</summary>
    [Fact(DisplayName = "Handle recusa código de tipo de etapa inexistente ou desativado sem persistir regra")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderComResposta(null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_NOVO"), repository, tipoReader, tipoEtapaReader, ModalidadeReaderViva(), TipoDocumentoReaderVivo(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// TipoEtapaCodigo é non-nullable só em compile-time: FluentValidation valida Predicado
    /// não-nulo, não os campos do subtipo polimórfico, e System.Text.Json não impõe NRT em
    /// runtime — um payload com <c>"tipoEtapaCodigo": null</c> desserializa sem erro.
    /// </summary>
    [Fact(DisplayName = "Handle recusa código de tipo de etapa nulo sem lançar")]
    public async Task Handle_TipoEtapaCodigoNulo_RecusaSemLancar()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));

        CriarObrigatoriedadeLegalCommand command = new(
            "PS_NOVO",
            CategoriaObrigatoriedade.Etapa,
            "REGRA_NOVA",
            new EtapaObrigatoria(null!),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            command, repository, tipoReader, tipoEtapaReader, ModalidadeReaderViva(), TipoDocumentoReaderVivo(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(
            "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco",
            "campo não preenchido é defeito da regra, não do catálogo — dizer que o tipo não existe mandaria procurar no lugar errado");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O reader normaliza a busca (<c>Trim</c>), mas se o valor persistido não fosse
    /// normalizado a regra ficaria aceita e, ao mesmo tempo, permanentemente inatingível em
    /// AvaliadorConformidadeLegal — que compara por igualdade ordinal exata.
    /// </summary>
    [Fact(DisplayName = "Handle normaliza espaços do código de tipo de etapa antes de persistir")]
    public async Task Handle_TipoEtapaCodigoComEspacos_PersisteNormalizado()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        CriarObrigatoriedadeLegalCommand command = new(
            "PS_NOVO",
            CategoriaObrigatoriedade.Etapa,
            "REGRA_NOVA",
            new EtapaObrigatoria($"  {TipoEtapaCodigo}  "),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            command, repository, tipoReader, tipoEtapaReader, ModalidadeReaderViva(), TipoDocumentoReaderVivo(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ObrigatoriedadeLegal>(regra =>
                ((EtapaObrigatoria)regra.Predicado).TipoEtapaCodigo == TipoEtapaCodigo),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Predicados que não são EtapaObrigatoria não consultam o reader de tipos de etapa.</summary>
    [Fact(DisplayName = "Handle com predicado que não é EtapaObrigatoria não consulta os tipos de etapa ativos")]
    public async Task Handle_PredicadoNaoEEtapaObrigatoria_NaoConsultaTiposDeEtapa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        CriarObrigatoriedadeLegalCommand command = new(
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            CategoriaObrigatoriedade.Outros,
            "REGRA_NOVA",
            new ConcorrenciaDuplaObrigatoria(),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            command, repository, tipoReader, tipoEtapaReader, ModalidadeReaderViva(), TipoDocumentoReaderVivo(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    private static CriarObrigatoriedadeLegalCommand NovaRegra(string tipoProcessoCodigo) => new(
        tipoProcessoCodigo,
        CategoriaObrigatoriedade.Etapa,
        "REGRA_NOVA",
        new EtapaObrigatoria(TipoEtapaCodigo),
        "Descrição da regra.",
        "Lei de teste.",
        new DateOnly(2026, 1, 1),
        null,
        null,
        null);

    [Fact(DisplayName = "Modalidade inexistente no predicado de documento é recusada sem persistir")]
    public async Task Handle_ModalidadeInexistente_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = TipoProcessoAtivo("PS_NOVO");
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new DocumentoObrigatorioParaModalidade("LB_PPl", "LAUDO_MEDICO")),
            repository, tipoReader, TipoEtapaReaderAtivo(),
            ModalidadeReaderSem("LB_PPl"), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.ModalidadeNaoEncontrada");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo de documento inexistente é recusado com erro distinto do de modalidade")]
    public async Task Handle_TipoDocumentoInexistente_RecusaComErroProprio()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_INEXISTENTE")),
            repository, TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderSem("LAUDO_INEXISTENTE"),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(
            "ObrigatoriedadeLegal.TipoDocumentoNaoEncontrado",
            "quem cadastra precisa saber qual dos dois códigos está errado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Um código órfão no meio da lista de modalidades mínimas recusa a regra inteira")]
    public async Task Handle_ModalidadesMinimasComCodigoOrfao_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new ModalidadesMinimas(["AC", "LB_PPl", "LB_Q"])),
            repository, TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderSem("LB_PPl"), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.ModalidadeNaoEncontrada");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Códigos do predicado são persistidos normalizados, não como vieram no payload")]
    public async Task Handle_CodigosComEspaco_PersisteNormalizado()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ObrigatoriedadeLegal? persistida = null;
        await repository.AdicionarAsync(Arg.Do<ObrigatoriedadeLegal>(r => persistida = r), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new DocumentoObrigatorioParaModalidade(" LB_PPI ", " LAUDO_MEDICO ")),
            repository, TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoObrigatorioParaModalidade predicado =
            persistida!.Predicado.Should().BeOfType<DocumentoObrigatorioParaModalidade>().Which;
        predicado.Modalidade.Should().Be("LB_PPI",
            "AvaliadorConformidadeLegal compara por igualdade ordinal contra o código congelado no processo");
        predicado.TipoDocumento.Should().Be("LAUDO_MEDICO");
    }

    [Fact(DisplayName = "Exigência de modalidades mínimas sem nenhum código é recusada pelo agregado")]
    public async Task Handle_ModalidadesMinimasVazia_Recusa()
    {
        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new ModalidadesMinimas([])),
            Substitute.For<IObrigatoriedadeLegalRepository>(), TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(
            "ObrigatoriedadeLegal.ModalidadesMinimasVazia",
            "lista vazia é aprovada por vacuidade pelo avaliador — a regra existiria sem exigir nada");
    }

    [Fact(DisplayName = "Código em branco no predicado recusa pela forma, não como código inexistente")]
    public async Task Handle_CodigoEmBranco_RecusaPelaFormaAntesDeConsultarOCadastro()
    {
        // Em branco vira busca por string vazia, que o cadastro legitimamente não encontra:
        // sem conferir a forma primeiro, a resposta diria "tipo de etapa não existe" e quem
        // cadastra procuraria o defeito no catálogo, não no campo que deixou vazio.
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new EtapaObrigatoria("   ")),
            Substitute.For<IObrigatoriedadeLegalRepository>(), TipoProcessoAtivo("PS_NOVO"), tipoEtapaReader,
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.PredicadoComCodigoEmBranco");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "Código de tipo de documento é buscado como o cadastro o grava, sem recompor a forma Unicode")]
    public async Task Handle_TipoDocumentoEmFormaDecomposta_BuscaOCodigoComoVeio()
    {
        // O cadastro de tipo de documento grava o código só aparado — sem NFC e sem formato
        // fechado. Recompor aqui faria a busca procurar um texto que o banco não guarda, e
        // um tipo existente seria recusado como inexistente.
        const string Decomposto = "LAUDO_ME\u0301DICO";
        ITipoDocumentoReader tipoDocumentoReader = Substitute.For<ITipoDocumentoReader>();
        tipoDocumentoReader.ObterVivoPorCodigoAsync(Decomposto, Arg.Any<CancellationToken>())
            .Returns(CadastrosVivos.TipoDocumento(Decomposto));

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new DocumentoObrigatorioParaModalidade("LB_PPI", $" {Decomposto} ")),
            Substitute.For<IObrigatoriedadeLegalRepository>(), TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), tipoDocumentoReader,
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipoDocumentoReader.Received().ObterVivoPorCodigoAsync(Decomposto, Arg.Any<CancellationToken>());
    }

    private static ITipoProcessoReader TipoProcessoAtivo(string codigo)
    {
        ITipoProcessoReader reader = Substitute.For<ITipoProcessoReader>();
        reader.ObterAtivoPorCodigoAsync(codigo, Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), codigo, "Processo novo", null));
        return reader;
    }

    private static CriarObrigatoriedadeLegalCommand RegraComPredicado(PredicadoObrigatoriedade predicado) =>
        new(
            TipoProcessoCodigo: "PS_NOVO",
            Categoria: CategoriaObrigatoriedade.Outros,
            RegraCodigo: "REGRA_NOVA",
            Predicado: predicado,
            DescricaoHumana: "Regra de teste",
            BaseLegal: "Lei 12.711/2012",
            VigenciaInicio: new DateOnly(2026, 1, 1),
            VigenciaFim: null,
            AtoNormativoUrl: null,
            PortariaInternaCodigo: null);


    [Fact(DisplayName = "Regra que exige tipo de deficiência inexistente é recusada na escrita")]
    public async Task Handle_TipoDeficienciaInexistente_Recusa()
    {
        // Antes, o código do predicado não era conferido contra cadastro nenhum: a regra
        // entrava, e só na publicação aparecia como necessidade não ofertada — mensagem
        // que descreve o processo seletivo e manda procurar o defeito no lugar errado.
        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new AtendimentoDisponivel(["TIPO_QUE_NAO_EXISTE"])),
            Substitute.For<IObrigatoriedadeLegalRepository>(), TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia("DEFICIENCIA_VISUAL"), CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoDeficienciaNaoEncontrada");
    }

    [Fact(DisplayName = "Regra que exige critério de desempate fora do catálogo é recusada na escrita")]
    public async Task Handle_CriterioDesempateInexistente_Recusa()
    {
        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new DesempateDeveIncluir("CRITERIO_INVENTADO")),
            Substitute.For<IObrigatoriedadeLegalRepository>(), TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate("IDADE_MAIOR"),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.CriterioDesempateNaoEncontrado");
    }

    [Fact(DisplayName = "Exigência de atendimento sem nenhum código é recusada pelo agregado")]
    public async Task Handle_AtendimentoDisponivelVazio_Recusa()
    {
        // Lista vazia é satisfeita por qualquer processo — a cláusula legal ficaria
        // aprovada por vacuidade, que é o oposto do que quem a cadastra pretende.
        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new AtendimentoDisponivel([])),
            Substitute.For<IObrigatoriedadeLegalRepository>(), TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.AtendimentoDisponivelVazio");
    }

    [Fact(DisplayName = "Código de tipo de deficiência com espaço supérfluo é normalizado antes de persistir")]
    public async Task Handle_CodigoComEspaco_Normaliza()
    {
        // O avaliador compara por igualdade exata contra o código congelado no processo:
        // gravar " DEFICIENCIA_VISUAL " passaria no cadastro e nunca casaria depois.
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ObrigatoriedadeLegal? persistida = null;
        await repository.AdicionarAsync(Arg.Do<ObrigatoriedadeLegal>(r => persistida = r), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new AtendimentoDisponivel([" DEFICIENCIA_VISUAL "])),
            repository, TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia("DEFICIENCIA_VISUAL"), CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        persistida.Should().NotBeNull();
        persistida!.Predicado.Should().BeOfType<AtendimentoDisponivel>()
            .Which.Necessidades.Should().Equal(["DEFICIENCIA_VISUAL"]);
    }

}
