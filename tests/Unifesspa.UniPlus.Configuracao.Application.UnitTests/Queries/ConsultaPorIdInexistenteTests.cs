namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;
using Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Campi;
using Unifesspa.UniPlus.Configuracao.Application.Queries.CategoriasDocumento;
using Unifesspa.UniPlus.Configuracao.Application.Queries.CondicoesAtendimento;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;
using Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.Queries.LocaisOferta;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Modalidades;
using Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

/// <summary>Leitura de um cadastro pelo Id: o Id que não existe é recusado com o código de não encontrado do cadastro.</summary>
public sealed class ConsultaPorIdInexistenteTests
{
    [Fact(DisplayName = "BaseLegalDoFada: o Id inexistente é recusado como não encontrado")]
    public async Task BaseLegalDoFada_IdInexistente_RecusaNaoEncontrado()
    {
        IBaseLegalDoFadaRepository repository = Substitute.For<IBaseLegalDoFadaRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BaseLegalDoFada?)null);

        Kernel.Results.Result<BaseLegalDoFadaDto> resultado = await ObterBaseLegalDoFadaPorIdQueryHandler.Handle(
            new ObterBaseLegalDoFadaPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("BaseLegalDoFada.NaoEncontrado");
    }

    [Fact(DisplayName = "CalendarioDiasUteis: o Id inexistente é recusado como não encontrado")]
    public async Task CalendarioDiasUteis_IdInexistente_RecusaNaoEncontrado()
    {
        ICalendarioDiasUteisRepository repository = Substitute.For<ICalendarioDiasUteisRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);

        Kernel.Results.Result<CalendarioDiasUteisDto> resultado = await ObterCalendarioDiasUteisPorIdQueryHandler.Handle(
            new ObterCalendarioDiasUteisPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CalendarioDiasUteis.NaoEncontrado");
    }

    [Fact(DisplayName = "Campus: o Id inexistente é recusado como não encontrado")]
    public async Task Campus_IdInexistente_RecusaNaoEncontrado()
    {
        ICampusRepository repository = Substitute.For<ICampusRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Campus?)null);

        Kernel.Results.Result<CampusDto> resultado = await ObterCampusPorIdQueryHandler.Handle(
            new ObterCampusPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Campus.NaoEncontrado");
    }

    [Fact(DisplayName = "CategoriaDocumento: o Id inexistente é recusado como não encontrado")]
    public async Task CategoriaDocumento_IdInexistente_RecusaNaoEncontrado()
    {
        ICategoriaDocumentoRepository repository = Substitute.For<ICategoriaDocumentoRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CategoriaDocumento?)null);

        Kernel.Results.Result<CategoriaDocumentoDto> resultado = await ObterCategoriaDocumentoPorIdQueryHandler.Handle(
            new ObterCategoriaDocumentoPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CategoriaDocumento.NaoEncontrada");
    }

    [Fact(DisplayName = "CondicaoAtendimento: o Id inexistente é recusado como não encontrado")]
    public async Task CondicaoAtendimento_IdInexistente_RecusaNaoEncontrado()
    {
        ICondicaoAtendimentoRepository repository = Substitute.For<ICondicaoAtendimentoRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CondicaoAtendimentoEspecializado?)null);

        Kernel.Results.Result<CondicaoAtendimentoDto> resultado = await ObterCondicaoAtendimentoPorIdQueryHandler.Handle(
            new ObterCondicaoAtendimentoPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoAtendimento.NaoEncontrada");
    }

    [Fact(DisplayName = "Curso: o Id inexistente é recusado como não encontrado")]
    public async Task Curso_IdInexistente_RecusaNaoEncontrado()
    {
        ICursoRepository repository = Substitute.For<ICursoRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Curso?)null);

        Kernel.Results.Result<CursoDto> resultado = await ObterCursoPorIdQueryHandler.Handle(
            new ObterCursoPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Curso.NaoEncontrado");
    }

    [Fact(DisplayName = "FaseCanonica: o Id inexistente é recusado como não encontrado")]
    public async Task FaseCanonica_IdInexistente_RecusaNaoEncontrado()
    {
        IFaseCanonicaRepository repository = Substitute.For<IFaseCanonicaRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FaseCanonica?)null);

        Kernel.Results.Result<FaseCanonicaDto> resultado = await ObterFaseCanonicaPorIdQueryHandler.Handle(
            new ObterFaseCanonicaPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCanonica.NaoEncontrada");
    }

    [Fact(DisplayName = "LocalOferta: o Id inexistente é recusado como não encontrado")]
    public async Task LocalOferta_IdInexistente_RecusaNaoEncontrado()
    {
        ILocalOfertaRepository repository = Substitute.For<ILocalOfertaRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LocalOferta?)null);

        Kernel.Results.Result<LocalOfertaDto> resultado = await ObterLocalOfertaPorIdQueryHandler.Handle(
            new ObterLocalOfertaPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("LocalOferta.NaoEncontrado");
    }

    [Fact(DisplayName = "Modalidade: o Id inexistente é recusado como não encontrado")]
    public async Task Modalidade_IdInexistente_RecusaNaoEncontrado()
    {
        IModalidadeRepository repository = Substitute.For<IModalidadeRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Modalidade?)null);

        Kernel.Results.Result<ModalidadeDto> resultado = await ObterModalidadePorIdQueryHandler.Handle(
            new ObterModalidadePorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Modalidade.NaoEncontrada");
    }

    [Fact(DisplayName = "OfertaCurso: o Id inexistente é recusado como não encontrado")]
    public async Task OfertaCurso_IdInexistente_RecusaNaoEncontrado()
    {
        IOfertaCursoRepository repository = Substitute.For<IOfertaCursoRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((OfertaCurso?)null);

        Kernel.Results.Result<OfertaCursoDto> resultado = await ObterOfertaCursoPorIdQueryHandler.Handle(
            new ObterOfertaCursoPorIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("OfertaCurso.NaoEncontrada");
    }

    [Fact(DisplayName = "FatoCandidato: o Id inexistente é recusado como não encontrado")]
    public async Task FatoCandidato_IdInexistente_RecusaNaoEncontrado()
    {
        IFatoCandidatoRepository repository = Substitute.For<IFatoCandidatoRepository>();
        repository.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FatoCandidato?)null);

        Kernel.Results.Result<FatoDeManutencaoDto> resultado = await ObterFatoCandidatoParaManutencaoQueryHandler.Handle(
            new ObterFatoCandidatoParaManutencaoQuery(Guid.NewGuid()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoCandidato.NaoEncontrado");
    }

    [Fact(DisplayName = "FatoCandidato: o código inexistente é recusado como não encontrado")]
    public async Task FatoCandidato_CodigoInexistente_RecusaNaoEncontrado()
    {
        IFatoCandidatoDoCatalogoReader reader = Substitute.For<IFatoCandidatoDoCatalogoReader>();
        reader.ObterPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((FatoDoCatalogoDto?)null);

        Kernel.Results.Result<FatoDoCatalogoDto> resultado = await ObterFatoCandidatoPorCodigoQueryHandler.Handle(
            new ObterFatoCandidatoPorCodigoQuery("CODIGO_INEXISTENTE"), reader, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FatoCandidato.NaoEncontrado");
    }
}
