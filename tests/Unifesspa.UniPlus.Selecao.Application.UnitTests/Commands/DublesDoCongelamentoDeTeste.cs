namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Os dublês das dependências dos handlers que congelam (Publicar, Retificar e FecharRetificacao) que quase
/// nenhum teste quer descrever: o processo no repositório, o documento do edital confirmado, o catálogo de tipos
/// de ato, o usuário autenticado e o canonicalizador que devolve um snapshot vazio.
/// </summary>
internal static class DublesDoCongelamentoDeTeste
{
    public static ISnapshotPublicacaoCanonicalizer CanonicalizerSubstituto(Action<EntradaCanonicalizacao>? captura = null)
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Do<EntradaCanonicalizacao>(e => captura?.Invoke(e)))
            .Returns(new SnapshotCanonico("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1"));
        return canonicalizer;
    }

    public static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repositorio;
    }

    public static IDocumentoAPublicarRepository RepositorioDeDocumento(Guid processoSeletivoId, params DocumentoAPublicar[] anexos)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoSeletivoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, ProcessoSeletivoConformeBuilder.HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
        repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(documento);
        repositorio.ListarDoProcessoAsync(processoSeletivoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(anexos);
        return repositorio;
    }

    public static ITipoAtoPublicadoReader TipoDeAtoReader()
    {
        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        reader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: false,
                EfeitoIrreversivel: false,
                EhResultado: false));
        return reader.ComQualquerTipoDeclarado();
    }

    public static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("user-sub-1");
        return contexto;
    }
}
