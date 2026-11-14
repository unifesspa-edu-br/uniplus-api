namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.LeitoresDeCadastroParaObrigatoriedadeLegal;

/// <summary>
/// A existência e a atividade do código de uma regra legal pertencem à Configuração;
/// a factory de domínio apenas preserva o código que o handler validou.
/// </summary>
public sealed class CriarObrigatoriedadeLegalCodigosDoPredicadoTests
{
    [Fact(DisplayName = "Modalidade inexistente no predicado de documento é recusada sem persistir")]
    public async Task Handle_ModalidadeInexistente_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = TipoProcessoAtivo("PS_NOVO");
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

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
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

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
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipoDocumentoReader.Received().ObterVivoPorCodigoAsync(Decomposto, Arg.Any<CancellationToken>());
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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

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
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        persistida.Should().NotBeNull();
        persistida!.Predicado.Should().BeOfType<AtendimentoDisponivel>()
            .Which.Necessidades.Should().Equal(["DEFICIENCIA_VISUAL"]);
    }

    [Fact(DisplayName = "Critério de desempate com caractere decomponível é encontrado como o catálogo o grava")]
    public async Task Handle_CriterioDesempateDecomposto_Encontra()
    {
        // O catálogo apara o código e não compõe. Se a conferência normalizasse em NFC,
        // um código gravado em forma decomposta seria procurado na forma composta — e
        // dado como inexistente, embora esteja lá.
        const string CodigoDecomposto = "CRITERIO_ANTIGUIDADE\u0301";

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            RegraComPredicado(new DesempateDeveIncluir($" {CodigoDecomposto} ")),
            Substitute.For<IObrigatoriedadeLegalRepository>(), TipoProcessoAtivo("PS_NOVO"), TipoEtapaReaderAtivo(),
            ModalidadeReaderViva(), TipoDocumentoReaderVivo(),
            CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(CodigoDecomposto),
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
