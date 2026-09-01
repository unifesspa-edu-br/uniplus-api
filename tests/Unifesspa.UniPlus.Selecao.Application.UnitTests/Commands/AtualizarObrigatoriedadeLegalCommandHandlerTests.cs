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
/// A atualização confere as referências do predicado com o mesmo rigor da criação:
/// aceitar numa o que a outra recusa seria deixar a regra inválida entrar pela porta
/// dos fundos, e uma regra que referencia cadastro inexistente é aprovada por
/// vacuidade na hora de publicar.
/// </summary>
public sealed class AtualizarObrigatoriedadeLegalCommandHandlerTests
{
    private const string ModalidadeAusente = "LB_PPl";
    private const string TipoDocumentoAusente = "LAUDO_INEXISTENTE";

    [Fact(DisplayName = "Trocar a modalidade do predicado por código inexistente recusa e preserva o predicado anterior")]
    public async Task Handle_ModalidadeInexistente_RecusaEPreservaPredicado()
    {
        ObrigatoriedadeLegal regra = RegraGravada();
        PredicadoObrigatoriedade predicadoAnterior = regra.Predicado;
        IObrigatoriedadeLegalRepository repository = RepositorioCom(regra);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result resultado = await AtualizarObrigatoriedadeLegalCommandHandler.Handle(
            ComPredicado(regra.Id, new DocumentoObrigatorioParaModalidade(ModalidadeAusente, "LAUDO_MEDICO")),
            repository,
            TipoProcessoAtivo(),
            CadastrosVivos.TiposEtapa(),
            ModalidadeReaderSem(ModalidadeAusente),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            unitOfWork,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.ModalidadeNaoEncontrada");
        regra.Predicado.Should().Be(
            predicadoAnterior,
            "a recusa acontece antes de Atualizar — a regra gravada não pode absorver o predicado inválido");
        repository.DidNotReceive().Atualizar(Arg.Any<ObrigatoriedadeLegal>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo de documento inexistente é recusado na atualização com o erro do seu próprio campo")]
    public async Task Handle_TipoDocumentoInexistente_RecusaComErroProprio()
    {
        ObrigatoriedadeLegal regra = RegraGravada();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result resultado = await AtualizarObrigatoriedadeLegalCommandHandler.Handle(
            ComPredicado(regra.Id, new DocumentoObrigatorioParaModalidade("LB_PPI", TipoDocumentoAusente)),
            RepositorioCom(regra),
            TipoProcessoAtivo(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.Modalidades(),
            TipoDocumentoReaderSem(TipoDocumentoAusente),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            unitOfWork,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(
            "ObrigatoriedadeLegal.TipoDocumentoNaoEncontrado",
            "quem corrige a regra precisa saber qual dos dois códigos do predicado está errado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Códigos com espaço supérfluo são gravados normalizados na atualização")]
    public async Task Handle_CodigosComEspaco_AtualizaNormalizado()
    {
        ObrigatoriedadeLegal regra = RegraGravada();
        IObrigatoriedadeLegalRepository repository = RepositorioCom(regra);

        Result resultado = await AtualizarObrigatoriedadeLegalCommandHandler.Handle(
            ComPredicado(regra.Id, new DocumentoObrigatorioParaModalidade(" LB_PPI ", " LAUDO_MEDICO ")),
            repository,
            TipoProcessoAtivo(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            Substitute.For<ISelecaoUnitOfWork>(),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoObrigatorioParaModalidade predicado =
            regra.Predicado.Should().BeOfType<DocumentoObrigatorioParaModalidade>().Which;
        predicado.Modalidade.Should().Be(
            "LB_PPI",
            "o código gravado com espaço nunca mais casaria com o congelado no processo, que a avaliação compara por igualdade ordinal");
        predicado.TipoDocumento.Should().Be("LAUDO_MEDICO");
    }

    private static ObrigatoriedadeLegal RegraGravada() =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "PS_NOVO",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "REGRA_GRAVADA",
            predicado: new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_MEDICO"),
            descricaoHumana: "Regra já gravada antes desta atualização",
            baseLegal: "Lei 12.711/2012",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

    private static IObrigatoriedadeLegalRepository RepositorioCom(ObrigatoriedadeLegal regra)
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        repository.ObterPorIdAsync(regra.Id, Arg.Any<CancellationToken>()).Returns(regra);
        repository.ExisteRegraCodigoAtivoAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        return repository;
    }

    private static ITipoProcessoReader TipoProcessoAtivo()
    {
        ITipoProcessoReader reader = Substitute.For<ITipoProcessoReader>();
        reader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));
        return reader;
    }

    private static IModalidadeReader ModalidadeReaderSem(string codigoAusente)
    {
        IModalidadeReader reader = Substitute.For<IModalidadeReader>();
        reader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>() == codigoAusente ? null : CadastrosVivos.Modalidade(call.Arg<string>()));
        return reader;
    }

    private static ITipoDocumentoReader TipoDocumentoReaderSem(string codigoAusente)
    {
        ITipoDocumentoReader reader = Substitute.For<ITipoDocumentoReader>();
        reader.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>() == codigoAusente ? null : CadastrosVivos.TipoDocumento(call.Arg<string>()));
        return reader;
    }

    private static AtualizarObrigatoriedadeLegalCommand ComPredicado(Guid id, PredicadoObrigatoriedade predicado) =>
        new(
            Id: id,
            TipoProcessoCodigo: "PS_NOVO",
            Categoria: CategoriaObrigatoriedade.Outros,
            RegraCodigo: "REGRA_GRAVADA",
            Predicado: predicado,
            DescricaoHumana: "Regra de teste",
            BaseLegal: "Lei 12.711/2012",
            VigenciaInicio: new DateOnly(2026, 1, 1),
            VigenciaFim: null,
            AtoNormativoUrl: null,
            PortariaInternaCodigo: null);

}
