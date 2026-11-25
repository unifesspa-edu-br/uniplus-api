namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A resolução dos valores selecionáveis (UNI-REQ-0072), atravessando um HANDLER
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
        // COR_RACA é coletado pela seção dos dados básicos do formulário de inscrição.
        processo.DefinirItens([]).IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Should().BeEmpty("pré-condição: COR_RACA não é citado em gatilho algum");

        FatoCandidatoView corRacaNoCatalogo = CorRacaNoCatalogo();

        EntradaCanonicalizacao entradaCapturada = await PublicarComFatoAsync(processo, corRacaNoCatalogo);
        ConferirValoresDeCorRaca(entradaCapturada);
    }

    // ── D1-ter — encoder é quem lança quando falta entrada; aqui provamos que o HANDLER sempre entrega uma ──

    [Fact(DisplayName = "Fato SIM_OU_NAO coletado recebe entrada null no dicionário — nunca fica de fora dele")]
    public async Task Publicar_ComFatoBooleanoColetado_EntradaENula()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        FatoCandidatoView baixaRendaNoCatalogo = PrepararBaixaRenda(processo);

        EntradaCanonicalizacao entradaCapturada = await PublicarComFatoAsync(processo, baixaRendaNoCatalogo);
        entradaCapturada.DadosDeCatalogo.ValoresSelecionaveisCongelados.Should().ContainKey("BAIXA_RENDA");
        entradaCapturada.DadosDeCatalogo.ValoresSelecionaveisCongelados!["BAIXA_RENDA"].Should().BeNull(
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
        OfertarLactanteEPcd(processo);

        FatoCandidatoView condicaoAtendimentoNoCatalogo = PrepararCondicaoAtendimento(processo);

        EntradaCanonicalizacao entradaCapturada = await PublicarComFatoAsync(processo, condicaoAtendimentoNoCatalogo);
        ConferirCondicoesOfertadas(entradaCapturada);
    }

    [Fact(DisplayName = "CA-06: remover uma condição ofertada e republicar preserva a ordem RELATIVA das restantes")]
    public async Task Publicar_AposRemoverUmaCondicaoOfertada_PreservaOrdemRelativaDasRestantes()
    {
        // Oferta original: PCD (0), LACTANTE (1), SABATISTA (2) — três condições, ordenadas por
        // código. Remover "LACTANTE" republicando só com PCD e SABATISTA tem de preservar a
        // ordem RELATIVA entre elas (PCD antes de SABATISTA), renumerando 0..1.
        ProcessoSeletivo processo = NovoProcessoConforme();
        OfertarPcdESabatista(processo);

        FatoCandidatoView condicaoAtendimentoNoCatalogo = PrepararCondicaoAtendimento(processo);

        EntradaCanonicalizacao entradaCapturada = await PublicarComFatoAsync(processo, condicaoAtendimentoNoCatalogo);
        entradaCapturada.DadosDeCatalogo.ValoresSelecionaveisCongelados!["CONDICAO_ATENDIMENTO"]!
            .Select(static v => v.Codigo).Should().Equal(
                [AtendimentoEspecializado.CodigoCondicaoPcd, "SABATISTA"],
                "'PCD' continua antes de 'SABATISTA' — a mesma ordem relativa de quando LACTANTE também existia");
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static FatoCandidatoView CorRacaNoCatalogo()
    {
        FatoValorDominioViewItem[] seisValores = ValoresDeCorRaca();
        FatoCandidatoView corRacaNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "COR_RACA", Nome: "Cor ou raça", Descricao: "Descrição do fato.",
            Dominio: CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), Origem: CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), Cardinalidade: CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ValoresDominio: [.. seisValores.Select(static v => v.Codigo)],
            PontoResolucao: "INSCRICAO", Binding: "CAMPO_FORMULARIO:COR_RACA",
            ValoresDominioDeclarados: seisValores, FonteValores: CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

        return corRacaNoCatalogo;
    }

    private static FatoCandidatoView PrepararBaixaRenda(ProcessoSeletivo processo)
    {
        FatoColetado baixaRenda = FatoColetadoDeTeste.Coletando("BAIXA_RENDA", 0).ComRotulo("Baixa renda").Construir();
        processo.DefinirItens([baixaRenda]).IsSuccess.Should().BeTrue();

        FatoCandidatoView baixaRendaNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "BAIXA_RENDA", Nome: "Baixa renda", Descricao: "Descrição do fato.",
            Dominio: CodigoComRotulo<TipoDoDado>.De(TipoDoDado.SimOuNao), Origem: CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), Cardinalidade: CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_FORMULARIO:BAIXA_RENDA",
            ValoresDominioDeclarados: null, FonteValores: null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

        return baixaRendaNoCatalogo;
    }

    private static void OfertarLactanteEPcd(ProcessoSeletivo processo)
    {
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar(
                condicoes: [
                    CondicaoOfertada.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante"),
                    CondicaoOfertada.Criar(Guid.CreateVersion7(), AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
                ],
                recursos: [],
                tiposDeficiencia: []).Value!).IsSuccess.Should().BeTrue();
    }

    private static void OfertarPcdESabatista(ProcessoSeletivo processo)
    {
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar(
                condicoes: [
                    CondicaoOfertada.Criar(Guid.CreateVersion7(), AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
                    CondicaoOfertada.Criar(Guid.CreateVersion7(), "SABATISTA", "Sabatista"),
                ],
                recursos: [],
                tiposDeficiencia: []).Value!).IsSuccess.Should().BeTrue();
    }

    private static FatoCandidatoView PrepararCondicaoAtendimento(ProcessoSeletivo processo)
    {
        FatoColetado condicaoAtendimento = FatoColetadoDeTeste.Coletando("CONDICAO_ATENDIMENTO", 0)
            .ComRotulo("Condição de atendimento")
            .DoTipo(TipoDeCampo.EscolhaMultipla)
            .Construir();
        processo.DefinirItens([condicaoAtendimento]).IsSuccess.Should().BeTrue();

        FatoCandidatoView condicaoAtendimentoNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "CONDICAO_ATENDIMENTO", Nome: "Condição de atendimento", Descricao: "Descrição do fato.",
            Dominio: CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), Origem: CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), Cardinalidade: CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores),
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_FORMULARIO:CONDICAO_ATENDIMENTO",
            ValoresDominioDeclarados: null, FonteValores: CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.OpcoesDoProcesso), Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

        return condicaoAtendimentoNoCatalogo;
    }

    private static (IDocumentoAPublicarRepository Repository, DocumentoAPublicar Documento) DocumentoConfirmado(ProcessoSeletivo processo)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoAPublicarRepository documentoRepository = Substitute.For<IDocumentoAPublicarRepository>();
        documentoRepository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);

        return (documentoRepository, documento);
    }

    private static ITipoAtoPublicadoReader TipoDeAtoDeAbertura()
    {
        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>().ComQualquerTipoDeclarado();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA", Nome: "Edital de abertura",
                CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));

        return tipoDeAtoReader;
    }

    private static void ConferirValoresDeCorRaca(EntradaCanonicalizacao? entradaCapturada)
    {
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.DadosDeCatalogo.ValoresSelecionaveisCongelados.Should().NotBeNull();
        entradaCapturada.DadosDeCatalogo.ValoresSelecionaveisCongelados.Should().ContainKey("COR_RACA");
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = entradaCapturada.DadosDeCatalogo.ValoresSelecionaveisCongelados!["COR_RACA"];
        valores.Should().NotBeNull("COR_RACA é fato de seleção — a entrada tem de ser um array, nunca null");
        valores!.Select(static v => v.Codigo).Should().BeEquivalentTo(
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"],
            "os 6 valores do catálogo têm de estar todos presentes, mesmo sem nenhuma condição de gatilho citando o fato");
    }

    private static async Task<EntradaCanonicalizacao> PublicarComFatoAsync(ProcessoSeletivo processo, FatoCandidatoView fato)
    {
        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo, e => entradaCapturada = e);
        CatalogoComFato(mocks, fato);
        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);
        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        return entradaCapturada!;
    }

    private static void CatalogoComFato(Mocks mocks, FatoCandidatoView fato) =>
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[fato]));

    private static void ConferirCondicoesOfertadas(EntradaCanonicalizacao? entradaCapturada)
    {
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            entradaCapturada!.DadosDeCatalogo.ValoresSelecionaveisCongelados!["CONDICAO_ATENDIMENTO"];

        valores.Should().NotBeNull();
        valores!.Should().HaveCount(2, "o processo oferta exatamente 2 das 3 condições — nunca uma terceira inventada");
        valores.Select(static v => v.Codigo).Should().Equal(
            ["LACTANTE", AtendimentoEspecializado.CodigoCondicaoPcd],
            "ordenado por CondicaoCodigo ordinal — 'LACTANTE' antecede 'PCD' alfabeticamente, mesmo tendo sido " +
            "inserido ANTES na oferta (a ordem de inserção não decide nada aqui)");
        valores.Select(static v => v.Ordem).Should().Equal([0, 1], "numerado 0..n-1 pela projeção, D3");
    }

    private static FatoValorDominioViewItem[] ValoresDeCorRaca()
    {
        return
        [
            new("BRANCA", "Branca.", 0, true),
            new("PRETA", "Preta.", 1, true),
            new("PARDA", "Parda.", 2, true),
            new("AMARELA", "Amarela.", 3, true),
            new("INDIGENA", "Indígena.", 4, true),
            new("NAO_INFORMADO", "Não informado.", 5, true),
        ];
    }

    private static ISnapshotPublicacaoCanonicalizer Canonicalizador(Action<EntradaCanonicalizacao>? captura)
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Do<EntradaCanonicalizacao>(e => captura?.Invoke(e)))
            .Returns(new SnapshotCanonico(EnvelopeProjetavelDeTeste.Bytes, "1.3", "canonical-json/sha256@v1"));

        return canonicalizer;
    }

    private static IObrigatoriedadeLegalRepository Obrigatoriedades()
    {
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);

        return obrigatoriedadeLegalRepository;
    }

    private static ProcessoSeletivo NovoProcessoConforme() =>
        ProcessoSeletivoConformeBuilder.Criar("PS Resolvedor de Valores Selecionáveis");

    private sealed record Mocks(
        IProcessoSeletivoRepository ProcessoRepository,
        IDocumentoAPublicarRepository DocumentoRepository,
        ISnapshotPublicacaoCanonicalizer Canonicalizer,
        ITipoAtoPublicadoReader TipoDeAtoReader,
        IObrigatoriedadeLegalRepository ObrigatoriedadeLegalRepository,
        IFatoCandidatoReader FatoCandidatoReader);

    private static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repository;
    }

    private static (Mocks Mocks, DocumentoAPublicar Documento) NovosMocks(ProcessoSeletivo processo, Action<EntradaCanonicalizacao>? captura = null)
    {
        (IDocumentoAPublicarRepository documentoRepository, DocumentoAPublicar documento) = DocumentoConfirmado(processo);
        return (new Mocks(
            RepositorioDoProcesso(processo),
            documentoRepository,
            Canonicalizador(captura),
            TipoDeAtoDeAbertura(),
            Obrigatoriedades(),
            Substitute.For<IFatoCandidatoReader>()), documento);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> HandleAsync(
        Mocks mocks, ProcessoSeletivo processo, DocumentoAPublicar documento) =>
        ServicosDoHandler(mocks).ComMotivosDeDecisaoDoProcesso(processo).PublicarAsync(ComandoDeAbertura(processo, documento));

    private static ServicosDoCongelamentoDeTeste ServicosDoHandler(Mocks mocks)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("user-conta-1");

        return new ServicosDoCongelamentoDeTeste()
            .ComRepositorio(mocks.ProcessoRepository)
            .ComDocumentos(mocks.DocumentoRepository)
            .ComCanonicalizer(mocks.Canonicalizer)
            .ComUsuario(userContext)
            .ComTiposDeAto(mocks.TipoDeAtoReader)
            .ComObrigatoriedadesLegais(mocks.ObrigatoriedadeLegalRepository)
            .ComCatalogoDeFatos(mocks.FatoCandidatoReader)
            .ComRelogio(TimeProvider.System);
    }

    private static PublicarProcessoSeletivoCommand ComandoDeAbertura(ProcessoSeletivo processo, DocumentoAPublicar documento) =>
        new(processo.Id, "001/2026", null, null,
            DocumentoEditalId: documento.Id,
            Ato: new DadosDoAto(
                Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataDoAto: new DateOnly(2026, 1, 1),
                Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA"));
}
