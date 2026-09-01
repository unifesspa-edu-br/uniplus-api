namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A resolução dos valores selecionáveis (issue #1059, UNI-REQ-0072), atravessando um HANDLER
/// REAL de publicação — nunca o canonicalizador com entrada já pronta (CA-01): é o handler quem
/// resolve o dicionário a partir do catálogo, e é essa resolução que este teste prova. O
/// canonicalizador é mockado apenas para não precisar dos demais 22 blocos do envelope — a
/// captura da <see cref="EntradaCanonicalizacao"/> que ele recebe é a asserção.
/// </summary>
public sealed class ValoresSelecionaveisResolverTests
{
    private static readonly string HashFixo = ProcessoSeletivoConformeBuilder.HashFixo;
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // ── CA-01 — completude: COR_RACA coletado, SEM gatilho, atravessando o handler real ──

    [Fact(DisplayName = "CA-01: processo que coleta COR_RACA sem citá-lo em gatilho — o envelope resolvido traz os 6 valores do catálogo")]
    public async Task Publicar_ComCorRacaColetadoSemGatilho_ResolveOsSeisValores()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        FatoColetado corRaca = FatoColetado.Criar(
            "COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, obrigatorio: true, null).Value!;
        processo.DefinirFatosColetados([corRaca], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Should().BeEmpty("pré-condição: COR_RACA não é citado em gatilho algum");

        FatoValorDominioViewItem[] seisValores =
        [
            new("BRANCA", "Branca.", 0, true),
            new("PRETA", "Preta.", 1, true),
            new("PARDA", "Parda.", 2, true),
            new("AMARELA", "Amarela.", 3, true),
            new("INDIGENA", "Indígena.", 4, true),
            new("NAO_INFORMADO", "Não informado.", 5, true),
        ];
        FatoCandidatoView corRacaNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "COR_RACA", Nome: "Cor ou raça", Descricao: null,
            Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
            ValoresDominio: [.. seisValores.Select(static v => v.Codigo)],
            PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:COR_RACA",
            ValoresDominioDeclarados: seisValores);

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[corRacaNoCatalogo]);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.ValoresSelecionaveisCongelados.Should().NotBeNull();
        entradaCapturada.ValoresSelecionaveisCongelados.Should().ContainKey("COR_RACA");
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = entradaCapturada.ValoresSelecionaveisCongelados!["COR_RACA"];
        valores.Should().NotBeNull("COR_RACA é fato de seleção — a entrada tem de ser um array, nunca null");
        valores!.Select(static v => v.Codigo).Should().BeEquivalentTo(
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"],
            "os 6 valores do catálogo têm de estar todos presentes, mesmo sem nenhuma condição de gatilho citando o fato");
    }

    // ── D1-ter — encoder é quem lança quando falta entrada; aqui provamos que o HANDLER sempre entrega uma ──

    [Fact(DisplayName = "Fato BOOLEANO coletado recebe entrada null no dicionário — nunca fica de fora dele")]
    public async Task Publicar_ComFatoBooleanoColetado_EntradaENula()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        FatoColetado baixaRenda = FatoColetado.Criar(
            "BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, obrigatorio: false, null).Value!;
        processo.DefinirFatosColetados([baixaRenda], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FatoCandidatoView baixaRendaNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "BAIXA_RENDA", Nome: "Baixa renda", Descricao: null,
            Dominio: "BOOLEANO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:BAIXA_RENDA",
            ValoresDominioDeclarados: null);

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[baixaRendaNoCatalogo]);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada!.ValoresSelecionaveisCongelados.Should().ContainKey("BAIXA_RENDA");
        entradaCapturada.ValoresSelecionaveisCongelados!["BAIXA_RENDA"].Should().BeNull(
            "BAIXA_RENDA não é fato de seleção — a bicondicional exige null, nunca um array (mesmo vazio)");
    }

    // ── CA-06 — escopo-processo: subconjunto ofertado, ordem atribuída e ordem relativa preservada ──

    [Fact(DisplayName = "CA-06: processo que oferta duas de três condições cadastradas expõe exatamente as duas, na ordem atribuída (por CondicaoCodigo)")]
    public async Task Publicar_ComCondicaoAtendimentoParcial_ExpoeExatamenteAsOfertadasNaOrdemAtribuida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        // Oferta DUAS das três condições "cadastradas" (o cadastro em si não participa deste
        // teste — a oferta é o que o processo já tem congelado por snapshot-copy): LACTANTE e
        // PCD, deliberadamente fora de ordem alfabética na oferta, para provar que é o CÓDIGO —
        // não a ordem de inserção na oferta — que decide a posição.
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                condicoes: [
                    OfertaCondicao.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante"),
                    OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
                ],
                recursos: [],
                tiposDeficiencia: []).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        FatoColetado condicaoAtendimento = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, obrigatorio: false, null).Value!;
        processo.DefinirFatosColetados([condicaoAtendimento], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        FatoCandidatoView condicaoAtendimentoNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "CONDICAO_ATENDIMENTO", Nome: "Condição de atendimento", Descricao: null,
            Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "MULTIVALORADO",
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:CONDICAO_ATENDIMENTO",
            ValoresDominioDeclarados: null);

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[condicaoAtendimentoNoCatalogo]);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            entradaCapturada!.ValoresSelecionaveisCongelados!["CONDICAO_ATENDIMENTO"];

        valores.Should().NotBeNull();
        valores!.Should().HaveCount(2, "o processo oferta exatamente 2 das 3 condições — nunca uma terceira inventada");
        valores.Select(static v => v.Codigo).Should().Equal(
            ["LACTANTE", OfertaAtendimentoEspecializado.CodigoCondicaoPcd],
            "ordenado por CondicaoCodigo ordinal — 'LACTANTE' antecede 'PCD' alfabeticamente, mesmo tendo sido " +
            "inserido ANTES na oferta (a ordem de inserção não decide nada aqui)");
        valores.Select(static v => v.Ordem).Should().Equal([0, 1], "numerado 0..n-1 pela projeção, D3");
    }

    [Fact(DisplayName = "CA-06: remover uma condição ofertada e republicar preserva a ordem RELATIVA das restantes")]
    public async Task Publicar_AposRemoverUmaCondicaoOfertada_PreservaOrdemRelativaDasRestantes()
    {
        // Oferta original: PCD (0), LACTANTE (1), SABATISTA (2) — três condições, ordenadas por
        // código. Remover "LACTANTE" republicando só com PCD e SABATISTA tem de preservar a
        // ordem RELATIVA entre elas (PCD antes de SABATISTA), renumerando 0..1.
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                condicoes: [
                    OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
                    OfertaCondicao.Criar(Guid.CreateVersion7(), "SABATISTA", "Sabatista"),
                ],
                recursos: [],
                tiposDeficiencia: []).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        FatoColetado condicaoAtendimento = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, obrigatorio: false, null).Value!;
        processo.DefinirFatosColetados([condicaoAtendimento], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        FatoCandidatoView condicaoAtendimentoNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "CONDICAO_ATENDIMENTO", Nome: "Condição de atendimento", Descricao: null,
            Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "MULTIVALORADO",
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:CONDICAO_ATENDIMENTO",
            ValoresDominioDeclarados: null);

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[condicaoAtendimentoNoCatalogo]);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada!.ValoresSelecionaveisCongelados!["CONDICAO_ATENDIMENTO"]!
            .Select(static v => v.Codigo).Should().Equal(
                [OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "SABATISTA"],
                "'PCD' continua antes de 'SABATISTA' — a mesma ordem relativa de quando LACTANTE também existia");
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static ProcessoSeletivo NovoProcessoConforme() =>
        ProcessoSeletivoConformeBuilder.Criar("PS Resolvedor de Valores Selecionáveis");

    private sealed record Mocks(
        IProcessoSeletivoRepository ProcessoRepository,
        IDocumentoEditalRepository DocumentoRepository,
        ISnapshotPublicacaoCanonicalizer Canonicalizer,
        ITipoAtoPublicadoReader TipoDeAtoReader,
        IVagaDeLinhagemReader VagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository ObrigatoriedadeLegalRepository,
        IFatoCandidatoReader FatoCandidatoReader);

    private static (Mocks Mocks, DocumentoEdital Documento) NovosMocks(ProcessoSeletivo processo, Action<EntradaCanonicalizacao>? captura = null)
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        documentoRepository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);

        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Do<EntradaCanonicalizacao>(e => captura?.Invoke(e)))
            .Returns(new SnapshotCanonico("{}"u8.ToArray(), "1.3", "canonical-json/sha256@v1"));

        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA", Nome: "Edital de abertura",
                CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false));

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);

        return (new Mocks(
            processoRepository,
            documentoRepository,
            canonicalizer,
            tipoDeAtoReader,
            Substitute.For<IVagaDeLinhagemReader>(),
            obrigatoriedadeLegalRepository,
            Substitute.For<IFatoCandidatoReader>()), documento);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> HandleAsync(
        Mocks mocks, ProcessoSeletivo processo, DocumentoEdital documento)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("user-sub-1");

        return PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataPublicacao: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA")),
            mocks.ProcessoRepository,
            mocks.DocumentoRepository,
            mocks.Canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            mocks.TipoDeAtoReader,
            mocks.VagaDeLinhagemReader,
            mocks.ObrigatoriedadeLegalRepository,
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            mocks.FatoCandidatoReader,
            CalendarioVigenteReaderDeTeste.SemVigente(),
            TimeProvider.System,
            CancellationToken.None);
    }
}
