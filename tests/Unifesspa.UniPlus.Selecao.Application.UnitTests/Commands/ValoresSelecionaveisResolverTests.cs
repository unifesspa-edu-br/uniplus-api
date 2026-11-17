namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
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

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.DublesDoCongelamentoDeTeste;

/// <summary>
/// A resolução dos valores selecionáveis (issue #1059, UNI-REQ-0072), atravessando um HANDLER
/// REAL de publicação — nunca o canonicalizador com entrada já pronta (CA-01): é o handler quem
/// resolve o dicionário a partir do catálogo, e é essa resolução que este teste prova. O
/// canonicalizador é mockado apenas para não precisar dos demais 22 blocos do envelope — a
/// captura da <see cref="EntradaCanonicalizacao"/> que ele recebe é a asserção.
/// </summary>
public sealed class ValoresSelecionaveisResolverTests
{

    [Fact(DisplayName = "COR_RACA coletado sem gatilho congela os seis valores do catálogo")]
    public async Task Publicar_ComCorRacaColetadoSemGatilho_ResolveOsSeisValores()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirItens([]).IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Should().BeEmpty("COR_RACA não é citado em gatilho algum");
        FatoCandidatoView fato = FatoNoCatalogo("COR_RACA", "Cor ou raça", "CATEGORICO", "ESCALAR", "GLOBAL", ValoresDaCorRaca);

        EntradaCanonicalizacao entrada = await PublicarComCatalogoAsync(processo, fato);

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = entrada.ValoresSelecionaveisCongelados!["COR_RACA"];
        valores.Should().NotBeNull();
        valores!.Select(static valor => valor.Codigo).Should().BeEquivalentTo(
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"]);
    }

    [Fact(DisplayName = "Fato booleano coletado recebe entrada nula no dicionário")]
    public async Task Publicar_ComFatoBooleanoColetado_EntradaENula()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        FatoColetado baixaRenda = FatoColetado.Criar(
            "BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItens([baixaRenda]).IsSuccess.Should().BeTrue();
        FatoCandidatoView fato = FatoNoCatalogo("BAIXA_RENDA", "Baixa renda", "BOOLEANO", "ESCALAR", fonte: null);

        EntradaCanonicalizacao entrada = await PublicarComCatalogoAsync(processo, fato);

        entrada.ValoresSelecionaveisCongelados.Should().ContainKey("BAIXA_RENDA");
        entrada.ValoresSelecionaveisCongelados!["BAIXA_RENDA"].Should().BeNull("o fato não é de seleção");
    }

    [Fact(DisplayName = "A publicação oferece exatamente as condições do processo, na ordem do código")]
    public async Task Publicar_ComCondicaoAtendimentoParcial_ExpoeExatamenteAsOfertadasNaOrdemAtribuida()
    {
        ProcessoSeletivo processo = ProcessoComCondicoesDeAtendimento(
            CondicaoOfertada.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante"),
            CondicaoOfertada.Criar(Guid.CreateVersion7(), AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"));

        EntradaCanonicalizacao entrada = await PublicarComCatalogoAsync(processo, FatoDaCondicaoDeAtendimento());

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = entrada.ValoresSelecionaveisCongelados!["CONDICAO_ATENDIMENTO"];
        valores.Should().NotBeNull();
        valores!.Should().HaveCount(2);
        valores.Select(static valor => valor.Codigo).Should().Equal(["LACTANTE", AtendimentoEspecializado.CodigoCondicaoPcd]);
        valores.Select(static valor => valor.Ordem).Should().Equal([0, 1]);
    }

    [Fact(DisplayName = "Remover uma condição ofertada preserva a ordem relativa das restantes")]
    public async Task Publicar_AposRemoverUmaCondicaoOfertada_PreservaOrdemRelativaDasRestantes()
    {
        ProcessoSeletivo processo = ProcessoComCondicoesDeAtendimento(
            CondicaoOfertada.Criar(Guid.CreateVersion7(), AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
            CondicaoOfertada.Criar(Guid.CreateVersion7(), "SABATISTA", "Sabatista"));

        EntradaCanonicalizacao entrada = await PublicarComCatalogoAsync(processo, FatoDaCondicaoDeAtendimento());

        entrada.ValoresSelecionaveisCongelados!["CONDICAO_ATENDIMENTO"]!.Select(static valor => valor.Codigo)
            .Should().Equal([AtendimentoEspecializado.CodigoCondicaoPcd, "SABATISTA"]);
    }

    private static readonly FatoValorDominioViewItem[] ValoresDaCorRaca =
    [
        new("BRANCA", "Branca.", 0, true),
        new("PRETA", "Preta.", 1, true),
        new("PARDA", "Parda.", 2, true),
        new("AMARELA", "Amarela.", 3, true),
        new("INDIGENA", "Indígena.", 4, true),
        new("NAO_INFORMADO", "Não informado.", 5, true),
    ];

    private static FatoCandidatoView FatoDaCondicaoDeAtendimento() =>
        FatoNoCatalogo("CONDICAO_ATENDIMENTO", "Condição de atendimento", "CATEGORICO", "MULTIVALORADO", "PROCESSO");

    private static FatoCandidatoView FatoNoCatalogo(
        string codigo, string nome, string dominio, string cardinalidade, string? fonte, IReadOnlyList<FatoValorDominioViewItem>? valores = null) => new(
        Id: Guid.CreateVersion7(), Codigo: codigo, Nome: nome, Descricao: "Descrição do fato.",
        Dominio: dominio, Origem: "DECLARADO", Cardinalidade: cardinalidade,
        ValoresDominio: valores is null ? null : [.. valores.Select(static valor => valor.Codigo)],
        PontoResolucao: "INSCRICAO", Binding: $"CAMPO_FORMULARIO:{codigo}",
        ValoresDominioDeclarados: valores, FonteValores: fonte, Ativo: true, ClassificacaoProtecao: "PESSOAL",
        HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static ProcessoSeletivo ProcessoComCondicoesDeAtendimento(params CondicaoOfertada[] condicoes)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar(condicoes, [], []).Value!).IsSuccess.Should().BeTrue();
        FatoColetado campo = FatoColetado.Criar(
            "CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
            finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItens([campo]).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static async Task<EntradaCanonicalizacao> PublicarComCatalogoAsync(ProcessoSeletivo processo, FatoCandidatoView fato)
    {
        EntradaCanonicalizacao? entrada = null;
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo, capturada => entrada = capturada);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[fato]));
        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);
        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entrada.Should().NotBeNull();
        entrada!.ValoresSelecionaveisCongelados.Should().NotBeNull();
        return entrada;
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static ProcessoSeletivo NovoProcessoConforme() =>
        ProcessoSeletivoConformeBuilder.Criar("PS Resolvedor de Valores Selecionáveis");

    private sealed record Mocks(
        IProcessoSeletivoRepository ProcessoRepository,
        IDocumentoAPublicarRepository DocumentoRepository,
        ISnapshotPublicacaoCanonicalizer Canonicalizer,
        ITipoAtoPublicadoReader TipoDeAtoReader,
        IObrigatoriedadeLegalRepository ObrigatoriedadeLegalRepository,
        IFatoCandidatoReader FatoCandidatoReader);

    private static (Mocks Mocks, DocumentoAPublicar Documento) NovosMocks(ProcessoSeletivo processo, Action<EntradaCanonicalizacao>? captura = null)
    {
        DocumentoAPublicar documento = DocumentoConfirmado(processo.Id);
        IObrigatoriedadeLegalRepository obrigatoriedades = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedades.ObterVigentesParaTipoProcessoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        IFatoCandidatoReader fatos = Substitute.For<IFatoCandidatoReader>();
        return (new Mocks(
            RepositorioDoProcesso(processo),
            RepositorioDoDocumento(documento),
            CanonicalizerSubstituto(captura, versao: "1.3"),
            TipoDeAtoReader(),
            obrigatoriedades,
            fatos), documento);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> HandleAsync(
        Mocks mocks, ProcessoSeletivo processo, DocumentoAPublicar documento) =>
        ServicosDoHandler(mocks).ComMotivosDoProcesso(processo).PublicarAsync(ComandoDeAbertura(processo, documento));

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
