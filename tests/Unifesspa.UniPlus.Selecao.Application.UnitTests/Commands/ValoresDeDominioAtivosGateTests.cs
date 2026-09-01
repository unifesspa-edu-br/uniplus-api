namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

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
/// O gate de valor inativo do domínio (issue #1059, UNI-REQ-0072): um fato coletado categórico
/// estático com valor inativo no catálogo, ou um predicado do processo que cite valor inativo,
/// recusa o congelamento — nos <b>três</b> handlers que congelam (ao lado de
/// <see cref="ColetabilidadeDeFatosGateTests"/>/<see cref="ConformidadeLegalGateTests"/>).
/// </summary>
public sealed class ValoresDeDominioAtivosGateTests
{
    private static readonly string HashFixo = ProcessoSeletivoConformeBuilder.HashFixo;
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly FatoValorDominioViewItem BrancaAtiva = new("BRANCA", "Branca.", 0, true);
    private static readonly FatoValorDominioViewItem PretaInativa = new("PRETA", "Preta.", 1, false);
    private static readonly FatoValorDominioViewItem PardaAtiva = new("PARDA", "Parda.", 2, true);

    private static FatoCandidatoView FatoCorRaca(params FatoValorDominioViewItem[] valores) => new(
        Id: Guid.CreateVersion7(),
        Codigo: "COR_RACA",
        Nome: "Cor ou raça",
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DECLARADO",
        Cardinalidade: "ESCALAR",
        ValoresDominio: [.. valores.Select(static v => v.Codigo)],
        PontoResolucao: "INSCRICAO",
        Binding: "CAMPO_INSCRICAO:COR_RACA",
        ValoresDominioDeclarados: valores);

    private static FatoColetado FatoColetadoCorRaca(IReadOnlyList<CondicaoPrecondicaoFato>? precondicoes = null) =>
        FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, obrigatorio: true, precondicoes).Value!;

    // ── CA-03(a) — fato coletado com valor inativo no seu próprio vocabulário ──

    [Fact(DisplayName = "CA-03(a): fato coletado categórico estático com valor inativo no vocabulário recusa citando fato")]
    public async Task Publicar_FatoColetadoComValorInativoNoVocabulario_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirFatosColetados([FatoColetadoCorRaca()], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        IFatoCandidatoReader reader = ReaderCom(FatoCorRaca(BrancaAtiva, PretaInativa, PardaAtiva));
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> eventos) = await Publicar(processo, reader, canonicalizer);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ValorDeDominioInativo");
        resposta.Error.Message.Should().Contain("COR_RACA");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
    }

    // ── CA-03(b) — predicado IGUAL citando valor inativo ──

    [Fact(DisplayName = "CA-03(b): predicado IGUAL citando valor inativo de fato categórico estático recusa")]
    public async Task Publicar_PredicadoIgualCitaValorInativo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!;
        DocumentoExigido exigencia = ExigenciaComGatilho(processo, condicao);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        IFatoCandidatoReader reader = ReaderCom(FatoCorRaca(BrancaAtiva, PretaInativa, PardaAtiva));
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ValorDeDominioInativo");
        resposta.Error.Message.Should().Contain("PRETA");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
    }

    // ── CA-03(c) — predicado EM com o inativo na SEGUNDA posição do array ──

    [Fact(DisplayName = "CA-03(c): predicado EM com o valor inativo na segunda posição do array recusa (um FirstOrDefault defeituoso passaria se estivesse na primeira)")]
    public async Task Publicar_PredicadoEmComInativoNaSegundaPosicao_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        // "BRANCA" (ativa) vem PRIMEIRO no array; "PRETA" (inativa) vem em SEGUNDO — uma
        // extração que parasse no primeiro item, ou que só olhasse Igual/Diferente, deixaria
        // passar em silêncio.
        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "COR_RACA", Operador.Em,
            JsonSerializer.SerializeToElement(new[] { "BRANCA", "PRETA" })).Value!;
        DocumentoExigido exigencia = ExigenciaComGatilho(processo, condicao);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        IFatoCandidatoReader reader = ReaderCom(FatoCorRaca(BrancaAtiva, PretaInativa, PardaAtiva));
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ValorDeDominioInativo");
        resposta.Error.Message.Should().Contain("PRETA");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
    }

    // ── CA-03(d) — todos ativos: publicação passa ──

    [Fact(DisplayName = "CA-03(d): todo valor ativo no vocabulário — publicação passa")]
    public async Task Publicar_TodosOsValoresAtivos_Aprova()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirFatosColetados([FatoColetadoCorRaca()], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        IFatoCandidatoReader reader = ReaderCom(FatoCorRaca(BrancaAtiva, PardaAtiva));
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        _ = canonicalizer.Received(1).Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
    }

    // ── Negativos de CA-03 ──

    [Fact(DisplayName = "Negativo: fato de escopo-processo coletado passa — não tem vocabulário global a inativar")]
    public async Task Publicar_FatoDeEscopoProcessoColetado_NaoBloqueiaMesmoComOutroFatoComValorInativo()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        // issue #1077: um fato coletável de escopo do processo sem NENHUM valor ofertado
        // recusaria a publicação (gate à parte deste, ProcessoSeletivo.
        // PendenciaDeFatoColetadoSemValoresOfertados) — a oferta precisa de ao menos uma
        // condição para que este teste continue isolando o que realmente quer provar (o
        // vocabulário inativo de OUTRO fato é irrelevante para um fato de escopo-processo).
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")], [], []).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        FatoColetado condicaoAtendimento = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, obrigatorio: false, null).Value!;
        processo.DefinirFatosColetados([condicaoAtendimento], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FatoCandidatoView condicaoAtendimentoNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "CONDICAO_ATENDIMENTO", Nome: "Condição de atendimento", Descricao: null,
            Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "MULTIVALORADO",
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:CONDICAO_ATENDIMENTO",
            ValoresDominioDeclarados: null);

        // COR_RACA (não coletado, não citado) tem valor inativo no catálogo — irrelevante
        // para este processo, que não o usa em lugar nenhum.
        IFatoCandidatoReader reader = ReaderCom(condicaoAtendimentoNoCatalogo, FatoCorRaca(BrancaAtiva, PretaInativa));
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    [Fact(DisplayName = "Negativo: predicado sobre fato booleano passa — booleano não entra no mapa de inativos")]
    public async Task Publicar_PredicadoSobreFatoBooleano_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "BAIXA_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        DocumentoExigido exigencia = ExigenciaComGatilho(processo, condicao);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        FatoCandidatoView baixaRenda = new(
            Id: Guid.CreateVersion7(), Codigo: "BAIXA_RENDA", Nome: "Baixa renda", Descricao: null,
            Dominio: "BOOLEANO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:BAIXA_RENDA",
            ValoresDominioDeclarados: null);

        // COR_RACA (não citado) tem valor inativo — irrelevante para este predicado booleano.
        IFatoCandidatoReader reader = ReaderCom(baixaRenda, FatoCorRaca(BrancaAtiva, PretaInativa));
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    [Fact(DisplayName = "Negativo: processo SEM fatos coletados ainda recusa inativo citado por documento exigido")]
    public async Task Publicar_SemFatosColetados_AindaRecusaInativoCitadoPorDocumento()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.FatosColetados.Should().BeEmpty("pré-condição: nenhum fato coletado");

        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!;
        DocumentoExigido exigencia = ExigenciaComGatilho(processo, condicao);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        IFatoCandidatoReader reader = ReaderCom(FatoCorRaca(BrancaAtiva, PretaInativa));
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsFailure.Should().BeTrue(
            "achado 3 do parecer: o passo 4 do gate é independente do passo 3 — um processo sem fatos coletados " +
            "ainda é recusado se um documento citar valor inativo");
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ValorDeDominioInativo");
    }

    [Fact(DisplayName = "Negativo: valor inativo de fato estático que o processo NÃO usa não bloqueia")]
    public async Task Publicar_ValorInativoDeFatoNaoUsado_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.FatosColetados.Should().BeEmpty();
        processo.DocumentosExigidos.Should().BeEmpty();

        // SEXO tem valor inativo no catálogo, mas o processo não coleta nem cita SEXO em
        // predicado nenhum — irrelevante.
        FatoCandidatoView sexo = new(
            Id: Guid.CreateVersion7(), Codigo: "SEXO", Nome: "Sexo", Descricao: null,
            Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
            ValoresDominio: ["FEMININO", "MASCULINO"], PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:SEXO",
            ValoresDominioDeclarados: [new("FEMININO", "Feminino.", 0, true), new("MASCULINO", "Masculino.", 1, false)]);

        IFatoCandidatoReader reader = ReaderCom(sexo);
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    // ── D4-bis: uma só leitura relevante do catálogo por congelamento ──

    [Fact(DisplayName = "D4-bis: o reader é consultado (ListarAsync) uma única vez — gate e resolvedores operam sobre o MESMO snapshot")]
    public async Task Publicar_ReaderConsultadoUmaUnicaVez()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirFatosColetados([FatoColetadoCorRaca()], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        // Um reader que devolvesse catálogos DIFERENTES em chamadas sucessivas provaria, se o
        // gate aprovasse sobre um e o resolvedor congelasse sobre outro, que há mais de uma
        // leitura relevante — aqui ele sempre devolve o mesmo catálogo (com valor ATIVO), então
        // qualquer resultado != sucesso indicaria uma leitura extra vendo outra coisa.
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[FatoCorRaca(BrancaAtiva, PardaAtiva)]);
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        await reader.Received(1).ListarAsync(Arg.Any<CancellationToken>());
        _ = await reader.DidNotReceive().ObterPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Correção 1 (revisão): fato de seleção sem origem conhecida de valores devolve 422 nomeado, nunca 500 ──

    [Fact(DisplayName = "Fato de seleção categórico DECLARADO sem valores no catálogo e sem ser CONDICAO_ATENDIMENTO/TIPO_DEFICIENCIA recusa com 422 nomeado, nunca lança")]
    public async Task Publicar_FatoDeSelecaoSemOrigemDeValores_RecusaComErroNomeado()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        // Um categórico DECLARADO novo (migration futura no catálogo), sem ValoresDominioDeclarados
        // (não é estático) e sem ser um dos dois categóricos de escopo-processo conhecidos.
        // CoerenciaDeRenderizacao só confere domínio/cardinalidade — SELECAO_UNICA é coerente com
        // CATEGORICO/ESCALAR mesmo sem origem de valores.
        FatoColetado fatoNovo = FatoColetado.Criar(
            "NOVO_CATEGORICO", 0, "Novo categórico", TipoRenderizacao.SelecaoUnica, obrigatorio: false, null).Value!;
        processo.DefinirFatosColetados([fatoNovo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FatoCandidatoView novoCategoricoNoCatalogo = new(
            Id: Guid.CreateVersion7(), Codigo: "NOVO_CATEGORICO", Nome: "Novo categórico", Descricao: null,
            Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
            ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:NOVO_CATEGORICO",
            ValoresDominioDeclarados: null);

        IFatoCandidatoReader reader = ReaderCom(novoCategoricoNoCatalogo);
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        Func<Task> publicar = async () => await Publicar(processo, reader, canonicalizer);

        // A garantia central da correção: nunca uma exceção não tratada (500).
        await publicar.Should().NotThrowAsync(
            "um fato de seleção sem origem conhecida de valores é um estado alcançável por dado (catálogo novo) — " +
            "tem de recusar com DomainError nomeado, nunca estourar");

        (Result resposta, IEnumerable<object> _) = await Publicar(processo, reader, canonicalizer);
        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoDeSelecaoSemOrigemDeValores");
        resposta.Error.Message.Should().Contain("NOVO_CATEGORICO");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static DocumentoExigido ExigenciaComGatilho(ProcessoSeletivo processo, CondicaoGatilho condicao) =>
        DocumentoExigido.Criar(
            processo.CronogramaFases.First().Id,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO",
            tipoDocumentoNome: "Declaração",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [condicao],
            basesLegais: [DocumentoExigidoBaseLegal.Criar(
                "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null).Value!;

    private static async Task<(Result Resposta, IEnumerable<object> Eventos)> Publicar(
        ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, ISnapshotPublicacaoCanonicalizer canonicalizer) =>
        await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            RepositorioDoProcesso(processo),
            RepositorioDeDocumento(processo.Id),
            canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            fatoCandidatoReader,
            CalendarioVigenteReaderDeTeste.SemVigente(),
            new RelogioFixo(Agora),
            CancellationToken.None);

    private static IFatoCandidatoReader ReaderCom(params FatoCandidatoView[] catalogo)
    {
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<FatoCandidatoView>)catalogo);
        return reader;
    }

    private static ISnapshotPublicacaoCanonicalizer CanonicalizerSubstituto()
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Any<EntradaCanonicalizacao>())
            .Returns(new SnapshotCanonico("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1"));
        return canonicalizer;
    }

    private static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repositorio;
    }

    private static IDocumentoEditalRepository RepositorioDeDocumento(Guid processoSeletivoId)
    {
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processoSeletivoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoEditalRepository repositorio = Substitute.For<IDocumentoEditalRepository>();
        repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(documento);
        return repositorio;
    }

    private static ITipoAtoPublicadoReader TipoDeAtoReader()
    {
        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        reader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: false,
                EfeitoIrreversivel: false));
        return reader;
    }

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("user-sub-1");
        return contexto;
    }

    private static DadosDoAto NovoAto() => new(
        Orgao: "CEPS",
        Serie: "EDITAL",
        Ano: 2026,
        DataPublicacao: new DateOnly(2026, 1, 1),
        Assinante: "Diretor do CEPS",
        TipoAtoCodigo: "EDITAL_ABERTURA");

    private static ProcessoSeletivo NovoProcessoConforme() =>
        ProcessoSeletivoConformeBuilder.Criar("PS Gate Valores de Domínio Ativos");

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
