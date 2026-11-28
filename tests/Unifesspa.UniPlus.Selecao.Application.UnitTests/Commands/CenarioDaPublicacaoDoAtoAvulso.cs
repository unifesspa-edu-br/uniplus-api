namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo, o catálogo e os documentos de teste da publicação do ato avulso, e a montagem da preparação com as
/// portas substituídas; cada classe de teste cria o seu.
/// </summary>
public sealed class CenarioDaPublicacaoDoAtoAvulso
{
    public static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly DataDoAto = new(2026, 12, 10);

    private static readonly Guid ProcessoDosDocumentos = Guid.CreateVersion7();

    public CenarioDaPublicacaoDoAtoAvulso()
    {
        Principal = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf", 'a');
        Anexo = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Docx, "quadro.docx", 'b');
        AnexoPdf = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Pdf, "tabela.pdf", 'd');
        OutroAnexoPdf = Documento(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Pdf, "outro.pdf", 'c');

        ConfigurarProcesso();
        Tipo(congela: false);
        ConfigurarDocumentos();
    }

    private void ConfigurarProcesso()
    {
        Processos.ObterSiglaDaUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns("CEPS");
        Processos.ObterAtosCriadoresAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([VersaoDivulgadaAtoId]);
        ConfigurarDivulgacao();
        Atos.ObterComAnexosAsync(VersaoDivulgadaAtoId, Arg.Any<CancellationToken>()).Returns(
            RegistroDoAto(VersaoDivulgadaAtoId, "EDITAL_ABERTURA"));
    }

    private void ConfigurarDocumentos()
    {
        Documentos.ListarDoProcessoAsync(ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Guid>)call[1])
                .SelectMany(id => new[] { Principal, Anexo, AnexoPdf, OutroAnexoPdf }.Where(d => d.Id == id)).ToList());
        Atos.ListarDocumentosJaUsadosAsync(ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
    }

    public Guid ProcessoId { get; } = Guid.CreateVersion7();
    public Guid VersaoDivulgadaAtoId { get; } = Guid.CreateVersion7();

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();
    public ICertameDivulgadoRepository Divulgados { get; } = Substitute.For<ICertameDivulgadoRepository>();
    public IAtoDoProcessoRepository Atos { get; } = Substitute.For<IAtoDoProcessoRepository>();
    public IDocumentoAPublicarRepository Documentos { get; } = Substitute.For<IDocumentoAPublicarRepository>();
    public ITipoAtoPublicadoReader Tipos { get; } = Substitute.For<ITipoAtoPublicadoReader>();
    public IRegistroDeAtoNormativo Registro { get; } = ServicosDoCongelamentoDeTeste.RegistroQueAceita();
    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public DocumentoAPublicar Principal { get; }
    public DocumentoAPublicar Anexo { get; }
    public DocumentoAPublicar AnexoPdf { get; }
    public DocumentoAPublicar OutroAnexoPdf { get; }

    public Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Publicar(
        PublicarAtoAvulsoDoProcessoCommand comando, TimeProvider? relogio = null, IResolvedorFusoInstitucional? fuso = null) =>
        PublicarAtoAvulsoDoProcessoCommandHandler.Handle(
            comando, Processos,
            new PreparacaoDaPublicacaoDoAtoAvulso(
                new ConferenciaDoPedidoDoAto(
                    new ConferenciaDoProcessoDivulgado(Processos, Divulgados),
                    new LeituraDoInstanteDoComando(relogio ?? new RelogioFixo(Agora), fuso ?? new ResolvedorFusoDeTeste()),
                    new ConferenciaDosDocumentosDoAto(Documentos, Atos)),
                new ConferenciaDoTipoDoAtoAvulso(new ConferenciaDoTipoDeAto(Tipos)),
                new ConferenciaDoAtoCorrigido(Atos, Processos)),
            new PublicacaoDoAtoDoProcesso(Atos, Registro, UnitOfWork), CancellationToken.None);

    public PublicarAtoAvulsoDoProcessoCommand Comando(
        Guid? principalId = null,
        IReadOnlyList<AnexoSolicitadoDoAto>? anexos = null,
        Guid? atoCorrigidoId = null,
        string? motivo = null,
        string tipo = "AVISO") =>
        new(ProcessoId, tipo, "5/2026", DataDoAto, "Diretor do CEPS", principalId ?? Principal.Id, anexos, atoCorrigidoId, motivo);

    /// <summary>Publica e confere uma única recusa, no campo dado, sem nada gravado nem enviado.</summary>
    public async Task DeveRecusar(PublicarAtoAvulsoDoProcessoCommand comando, string codigoEsperado, string? campoEsperado = null)
    {
        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(comando);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be(campoEsperado);
        resposta.Error!.Code.Should().Be(codigoEsperado);
        mensagens.Should().BeEmpty("a recusa acontece antes de qualquer envio ao registro central");
        await NadaFoiEscrito();
    }

    public async Task NadaFoiEscrito()
    {
        await Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await Registro.DidNotReceive().RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>());
    }

    public void Tipo(bool congela, bool unico = false) =>
        Tipos.ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>()).Returns(
            new TipoAtoPublicadoView("AVISO", "Aviso", congela, unico, false, false, PublicavelComoAvulso: true));

    /// <summary>Um ato do processo dado, registrado na Seleção; o que congela é um dos atos criadores de versão.</summary>
    public Guid AtoPublicadoDoProcesso(Guid processoId, bool aoCongelar = false)
    {
        AtoDoProcesso ato = RegistroDoAto(Guid.CreateVersion7(), "AVISO", processoId);
        Atos.ObterProcessoDoAtoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(processoId);
        if (aoCongelar)
        {
            Processos.ObterAtosCriadoresAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([VersaoDivulgadaAtoId, ato.Id]);
        }

        return ato.Id;
    }

    /// <summary>Um ato do processo que pode ser corrigido: do mesmo processo, que não congela e com cadeia de um só ato.</summary>
    public Guid AtoCorrigivel()
    {
        Guid ato = AtoPublicadoDoProcesso(ProcessoId);
        Atos.ListarCadeiaDeCorrecaoAsync(ato, Arg.Any<CancellationToken>()).Returns([ato]);
        return ato;
    }

    public AtoDoProcesso RegistroDoAto(Guid atoId, string tipo, Guid? processoId = null) => AtoDoProcesso.Registrar(
        new NovoAtoDoProcesso(
            atoId, processoId ?? ProcessoId, tipo, tipo, "1/2026", DataDoAto, "Diretor do CEPS", "ceps", "psm-medicina-2027",
            Guid.CreateVersion7(), []),
        Agora).Value!;

    public void SemPaginaPublica() =>
        Divulgados.ObterParaMutacaoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns((CertameDivulgado?)null);

    private void ConfigurarDivulgacao() =>
        Divulgados.ObterParaMutacaoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(
            CertameDivulgado.Criar(
                ProcessoId, 1, VersaoDivulgadaAtoId, new string('a', 64), "1",
                new FacetasDoCertameDivulgado("psm-medicina-2027", "PSM Medicina 2027", "001/2026", [], Agora.AddDays(-1), Agora.AddDays(20)),
                """{"nome":"documento"}""", Agora));

    public static DocumentoAPublicar Documento(FinalidadeDoDocumento finalidade, string tipoDeMidia, string nome, char hash)
    {
        // O processo do documento é o do teste; o handler pede os documentos pelo processo.
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoDosDocumentos, finalidade, nome, tipoDeMidia, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string(hash, 64), new RelogioFixo(Agora)).IsSuccess.Should().BeTrue();
        return documento;
    }
}
