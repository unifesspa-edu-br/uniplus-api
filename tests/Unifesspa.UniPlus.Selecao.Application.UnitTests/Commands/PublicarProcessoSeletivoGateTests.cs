namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Xunit;

/// <summary>
/// <b>Contraprova do CA-10 (ADR-0109 D5):</b> o gate precede a canonicalização.
/// </summary>
/// <remarks>
/// <para>
/// Um processo não conforme <b>não chega a ser projetado</b>. O canonicalizador
/// substituto registra a invocação — se ele for chamado, o teste falha.
/// </para>
/// <para>
/// Por que importa: sem o gate antecipado, a projeção de uma dimensão obrigatória
/// ausente <b>lançaria</b> (ADR-0109 D8) em vez de devolver o <c>DomainError</c> que
/// o contrato HTTP promete. O 422 viraria 500.
/// </para>
/// </remarks>
public sealed class PublicarProcessoSeletivoGateTests
{
    /// <summary>Canonicalizador espião — registra se foi invocado.</summary>
    private sealed class CanonicalizerEspiao : ISnapshotPublicacaoCanonicalizer
    {
        public bool FoiInvocado { get; private set; }

        public SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada)
        {
            FoiInvocado = true;
            return new SnapshotCanonico("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1");
        }
    }

    [Fact(DisplayName = "Publicar_ProcessoNaoConforme_NaoCanonicaliza — o gate precede a projeção (CA-10)")]
    public async Task Publicar_ProcessoNaoConforme_NaoCanonicaliza()
    {
        // Processo em rascunho, SEM nenhuma dimensão configurada — não conforme.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TipoProcesso.SiSU);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        documentoRepository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);

        CanonicalizerEspiao canonicalizer = new();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("teste");

        // As conferências que precedem o gate no handler (tipo de ato, vaga de
        // linhagem) têm de PASSAR — senão o teste provaria a recusa errada e o
        // canonicalizador ficaria sem ser chamado por outro motivo.
        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: true,
                EfeitoIrreversivel: false));

        IVagaDeLinhagemReader vagaDeLinhagemReader = Substitute.For<IVagaDeLinhagemReader>();

        (Result resposta, IEnumerable<object> eventos) = await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                ProcessoSeletivoId: processo.Id,
                Numero: "001/2026",
                PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
                PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
                DocumentoEditalId: documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS",
                    Serie: "EDITAL",
                    Ano: 2026,
                    DataPublicacao: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS",
                    TipoAtoCodigo: "EDITAL_ABERTURA")),
            processoRepository,
            documentoRepository,
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            tipoDeAtoReader,
            vagaDeLinhagemReader,
            TimeProvider.System,
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            "o contrato HTTP não muda — continua sendo o mesmo 422 de sempre");

        canonicalizer.FoiInvocado.Should().BeFalse(
            "um processo não conforme NÃO chega a ser canonicalizado (ADR-0109 D5). Sem o gate antecipado, a " +
            "projeção de uma dimensão obrigatória ausente lançaria (D8) e o 422 viraria 500.");

        eventos.Should().BeEmpty();
    }
}
