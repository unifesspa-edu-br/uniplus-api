namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirOpcoesDeclaradasCommandHandlerTests
{
    [Theory]
    [InlineData(" CATEGORIA_RENDA")]
    [InlineData("CATEGORIA_RENDA ")]
    [InlineData("categoria_renda")]
    public async Task Handle_CodigoDeFatoDiferenteDoCatalogo_Recusa(string codigo)
    {
        ProcessoSeletivo processo = CriarProcesso();
        Result resultado = await DefinirOpcoesDeclaradasCommandHandler.Handle(
            new DefinirOpcoesDeclaradasCommand(processo.Id, codigo, [new OpcaoDeclaradaInput("RURAL", "Rural")]),
            CriarRepositorio(processo), CriarReader(), Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.Error!.Code.Should().Be("OpcaoDeclaradaFato.FonteNaoEhDoProcesso");
        resultado.Error.Message.Should().NotContain(codigo);
        processo.OpcoesDeclaradas.Should().BeEmpty();
    }

    [Theory(DisplayName = "Declarar opções para fato cuja fonte não é o processo, fora do catálogo ou agregado de grupo é recusado")]
    [InlineData(FonteValoresFato.Catalogo, "CAMPO_FORMULARIO:COR_RACA")]
    [InlineData(null, "CAMPO_FORMULARIO:COR_RACA")]
    [InlineData(FonteValoresFato.OpcoesDoProcesso, "RESUMO_DE_GRUPO:COR_RACA_MEMBRO")]
    public async Task Handle_FonteQueNaoEhDoProcesso_Recusa(FonteValoresFato? fonte, string binding)
    {
        ProcessoSeletivo processo = CriarProcesso();
        IProcessoSeletivoRepository repositorio = CriarRepositorio(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ObterPorCodigoAsync("COR_RACA", Arg.Any<CancellationToken>()).Returns(fonte is null
            ? null
            : new FatoCandidatoView(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado),
                CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), ["PRETA"], "INSCRICAO", binding, null, (fonte is { } fonteInformada ? CodigoComRotulo<FonteValoresFato>.De(fonteInformada) : null), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true));

        Result resultado = await DefinirOpcoesDeclaradasCommandHandler.Handle(
            new DefinirOpcoesDeclaradasCommand(processo.Id, "COR_RACA", [new OpcaoDeclaradaInput("PRETA", "Preta")]),
            repositorio, reader, Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.Error!.Code.Should().Be("OpcaoDeclaradaFato.FonteNaoEhDoProcesso");
        processo.OpcoesDeclaradas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retirar do fato de membro uma opção que a condição de um agregado cita é recusado")]
    public async Task Handle_OpcaoCitadaPeloAgregado_Recusa()
    {
        ProcessoSeletivo processo = CriarProcesso();
        PrepararRegras(processo);
        IProcessoSeletivoRepository repositorio = CriarRepositorio(processo);
        IFatoCandidatoReader reader = CriarReader();

        Result resultado = await DefinirOpcoesDeclaradasCommandHandler.Handle(
            new DefinirOpcoesDeclaradasCommand(processo.Id, "CATEGORIA_RENDA", [new OpcaoDeclaradaInput("URBANA", "Urbana")]),
            repositorio, reader, Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.Error!.Code.Should().Be(OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva);
    }
    [Fact]
    public async Task Handle_OpcaoSemRotulo_ApontaOCampoDaOpcao()
    {
        ProcessoSeletivo processo = CriarProcesso();
        IProcessoSeletivoRepository repositorio = CriarRepositorio(processo);
        DefinirOpcoesDeclaradasCommand comando = new(processo.Id, "CATEGORIA_RENDA", [new("RURAL", "")]);

        Result resultado = await DefinirOpcoesDeclaradasCommandHandler.Handle(
            comando, repositorio, Substitute.For<IFatoCandidatoReader>(), Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("opcoes[0].rotulo");
    }

    [Fact(DisplayName = "Opção sem rótulo é recusada em rascunho antes de ler o catálogo de dados do candidato")]
    public async Task Handle_OpcaoSemRotulo_RecusaSemLerOCatalogo()
    {
        ProcessoSeletivo processo = CriarProcesso();
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();

        Result resultado = await HandleOpcaoSemRotuloAsync(CriarRepositorio(processo), reader, processo.Id);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("opcoes[0].rotulo");
        reader.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Processo inexistente é recusado antes da conferência das opções, sem ler o catálogo de dados do candidato")]
    public async Task Handle_ProcessoInexistenteComOpcaoSemRotulo_RecusaPeloProcesso()
    {
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();

        Result resultado = await HandleOpcaoSemRotuloAsync(Substitute.For<IProcessoSeletivoRepository>(), reader, Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        reader.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Processo publicado é recusado antes da conferência das opções, sem ler o catálogo de dados do candidato")]
    public async Task Handle_ProcessoPublicadoComOpcaoSemRotulo_RecusaPelaSituacao()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();

        Result resultado = await HandleOpcaoSemRotuloAsync(CriarRepositorio(processo), reader, processo.Id);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        reader.ReceivedCalls().Should().BeEmpty();
    }

    private static Task<Result> HandleOpcaoSemRotuloAsync(IProcessoSeletivoRepository repositorio, IFatoCandidatoReader reader, Guid processoId) =>
        DefinirOpcoesDeclaradasCommandHandler.Handle(
            new DefinirOpcoesDeclaradasCommand(processoId, "CATEGORIA_RENDA", [new("RURAL", "")]),
            repositorio, reader, Substitute.For<IUnitOfWork>(), CancellationToken.None);

    private static ProcessoSeletivo CriarProcesso() => ProcessoSeletivo.Criar(
        "PS Opções", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static void PrepararRegras(ProcessoSeletivo processo)
    {
        processo.DefinirOpcoesDeclaradas(
            "CATEGORIA_RENDA", [OpcaoDeclaradaFato.Criar("CATEGORIA_RENDA", "RURAL", "Rural", 0).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar("RENDA_RURAL",
            [
                RegraDerivacaoConfigurada.Criar(0, "SIM",
                    [CondicaoRegraDerivacao.Criar(0, "CATEGORIAS_RENDA_FAMILIA", Operador.AlgumDe, JsonSerializer.SerializeToElement(new[] { "RURAL" })).Value!]).Value!,
            ]).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    private static IFatoCandidatoReader CriarReader()
    {
        FatoCandidatoView membro = new(Guid.CreateVersion7(), "CATEGORIA_RENDA", "Categoria de renda", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado),
            CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), null, "HABILITACAO", "CAMPO_FORMULARIO:CATEGORIA_RENDA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.OpcoesDoProcesso), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: CodigoComRotulo<EscopoFato>.De(CatalogoDe<EscopoFato>.Obter("MEMBRO_GRUPO")), ProtecaoAprovada: true);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ObterPorCodigoAsync("CATEGORIA_RENDA", Arg.Any<CancellationToken>()).Returns(membro);
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
        [
            membro,
            new FatoCandidatoView(Guid.CreateVersion7(), "CATEGORIAS_RENDA_FAMILIA", "Categorias de renda da família", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao),
                CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores), null, "HABILITACAO", "RESUMO_DE_GRUPO:CATEGORIA_RENDA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.OpcoesDoProcesso), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        ]));

        return reader;
    }
    private static IProcessoSeletivoRepository CriarRepositorio(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repositorio;
    }
}
