namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os descritivos dos cadastros entram nos pontos em que o esquema <c>v1</c> real os declara só de leitura, e o documento
/// descrito continua válido contra ele.
/// </summary>
public sealed class DescritivosDosCadastrosNoEsquemaRealTests
{
    private const string Documento = """
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "cronograma": { "fases": [ { "chave": "fase_inscricao", "tipoFaseCodigo": "INSCRICAO",
            "publicacoesPrevistas": [ { "atoCodigo": "EDITAL_ABERTURA" } ],
            "bancasRequeridas": [ { "chave": "banca_documental", "tipoBancaCodigo": "BANCA_ANALISE_DOCUMENTAL", "categoriasDocumentoCodigos": [ "RENDA" ] } ] } ] },
          "etapas": [ { "chave": "etapa_prova", "tipoEtapaCodigo": "PROVA_OBJETIVA",
            "publicacoesPrevistas": [ { "atoCodigo": "EDITAL_ABERTURA" } ],
            "bancas": [ { "chave": "banca_recursos", "tipoBancaCodigo": "BANCA_ANALISE_RECURSOS" } ] } ],
          "distribuicaoVagas": [ { "ofertaCursoCodigo": "MEDICINA_MARABA", "modalidades": [ { "codigo": "AMPLA_CONCORRENCIA" } ] } ],
          "atendimento": { "condicoes": [ { "condicaoCodigo": "LACTANTE" } ], "recursos": [ { "recursoCodigo": "LEDOR" } ],
            "tiposDeficiencia": [ { "tipoDeficienciaCodigo": "DEFICIENCIA_VISUAL" } ] },
          "fada": { "aplica": true, "regra": { "codigo": "FADA_REGIONAL", "versao": "v1" }, "fator": 1.1, "baseLegalCodigo": "LEI_ESTADUAL_EXEMPLO" } }
        """;

    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    private static async Task<JsonObject> DescreverAsync()
    {
        JsonObject documento = JsonNode.Parse(Documento)!.AsObject();
        await new DescricaoDoDocumentoDoRascunho(Esquema, [.. DescricoesDoCronograma(), .. DescricoesDaOferta(), .. DescricoesDoAtendimento(), DescricaoDaFada()])
            .DescreverAsync(documento, CancellationToken.None);
        return documento;
    }

    [Fact(DisplayName = "O documento descrito pelos cadastros continua válido contra o esquema v1")]
    public async Task Descrever_DocumentoCompleto_ValidaContraOEsquema()
    {
        JsonObject documento = await DescreverAsync();

        Esquema.Avaliar(documento).Should().BeEmpty();
    }

    [Theory(DisplayName = "Cada descritivo entra no ponto que o esquema v1 declara só de leitura, ao lado do código")]
    [InlineData("cronograma/fases/0/tipoFaseNome", "Inscrição")]
    [InlineData("cronograma/fases/0/publicacoesPrevistas/0/atoNome", "Edital de abertura")]
    [InlineData("cronograma/fases/0/bancasRequeridas/0/tipoBancaNome", "Banca de análise documental")]
    [InlineData("cronograma/fases/0/bancasRequeridas/0/categoriasDocumentoNomes/0", "Renda familiar")]
    [InlineData("etapas/0/tipoEtapaNome", "Prova objetiva")]
    [InlineData("etapas/0/publicacoesPrevistas/0/atoNome", "Edital de abertura")]
    [InlineData("etapas/0/bancas/0/tipoBancaNome", "Banca de análise de recursos")]
    [InlineData("distribuicaoVagas/0/cursoNome", "Medicina")]
    [InlineData("distribuicaoVagas/0/cursoGrau", "Bacharelado")]
    [InlineData("distribuicaoVagas/0/unidadeOfertanteNome", "Campus Universitário de Marabá")]
    [InlineData("distribuicaoVagas/0/turnos/1", "VESPERTINO")]
    [InlineData("distribuicaoVagas/0/modalidades/0/descricao", "Vagas sem reserva")]
    [InlineData("atendimento/condicoes/0/condicaoNome", "Lactante")]
    [InlineData("atendimento/recursos/0/recursoNome", "Ledor")]
    [InlineData("atendimento/tiposDeficiencia/0/tipoDeficienciaNome", "Deficiência visual")]
    [InlineData("fada/tipoInstrumento", "LEI")]
    [InlineData("fada/identificacao", "Lei estadual de exemplo")]
    [InlineData("fada/descricao", "Atenua as desigualdades de acesso na região.")]
    [InlineData("fada/municipios/0/codigoIbge", "1504208")]
    [InlineData("fada/municipios/0/nome", "Marabá")]
    [InlineData("fada/municipios/0/uf", "PA")]
    public async Task Descrever_DocumentoCompleto_TrazODescritivoNoPontoDoEsquema(string caminho, string esperado)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        JsonObject documento = await DescreverAsync();

        Em(documento, caminho).Should().Be(esperado);
    }

    private static string Em(JsonNode documento, string caminho)
    {
        JsonNode? no = documento;
        foreach (string passo in caminho.Split('/'))
        {
            no = int.TryParse(passo, out int indice) ? no![indice] : no![passo];
        }

        return no!.GetValue<string>();
    }

    private static IDescricaoDeCadastro[] DescricoesDoCronograma()
    {
        IFaseCanonicaReader fases = Substitute.For<IFaseCanonicaReader>();
        fases.ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new FaseCanonicaView(Guid.CreateVersion7(), "INSCRICAO", "Inscrição", null, false, false, null, true, false, false, "NO_EDITAL")]);
        ITipoAtoPublicadoReader atos = Substitute.For<ITipoAtoPublicadoReader>();
        atos.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new TipoAtoPublicadoView("EDITAL_ABERTURA", "Edital de abertura", false, false, false, false)]);
        return [new DescricaoDeTiposDeFase(fases), new DescricaoDeTiposDeAto(atos, LeituraDoInstante()), DescricaoDosTiposDeBanca(), DescricaoDasCategorias(), DescricaoDosTiposDeEtapa()];
    }

    private static LeituraDoInstanteDoComando LeituraDoInstante()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return new LeituraDoInstanteDoComando(new RelogioFixo(new DateTimeOffset(2026, 11, 26, 15, 0, 0, TimeSpan.Zero)), fuso);
    }

    private static DescricaoDeTiposDeBanca DescricaoDosTiposDeBanca()
    {
        ITipoBancaReader tipos = Substitute.For<ITipoBancaReader>();
        tipos.ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(
        [
            new TipoBancaView(Guid.CreateVersion7(), TipoDeBanca.AnaliseDocumental, "Banca de análise documental", null, null, PapelDaBanca.AvaliaOMerito, false, false, []),
            new TipoBancaView(Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursos, "Banca de análise de recursos", null, null, PapelDaBanca.JulgaAPrimeiraInstancia, false, false, []),
        ]);
        return new(tipos);
    }

    private static DescricaoDeCategoriasDeDocumento DescricaoDasCategorias()
    {
        ICategoriaDocumentoReader categorias = Substitute.For<ICategoriaDocumentoReader>();
        categorias.ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new CategoriaDocumentoView(Guid.CreateVersion7(), "RENDA", "Renda familiar", null, 1)]);
        return new(categorias);
    }

    private static DescricaoDeTiposDeEtapa DescricaoDosTiposDeEtapa()
    {
        ITipoEtapaReader tipos = Substitute.For<ITipoEtapaReader>();
        tipos.ListarAtivosAsync(Arg.Any<CancellationToken>()).Returns(
            [new TipoEtapaView(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova objetiva", null, new AvaliacaoDasEtapasView(true, true, false, false, []))]);
        return new(tipos);
    }

    private static IDescricaoDeCadastro[] DescricoesDaOferta() =>
    [
        new DescricaoDeOfertasDeCurso(new Dictionary<string, OfertaDeCursoCitada> { ["MEDICINA_MARABA"] = OfertaDeMedicina() }),
        new DescricaoDeModalidades(new Dictionary<string, ModalidadeNoCadastro>
        {
            ["AMPLA_CONCORRENCIA"] = ModalidadesNoCadastroDeTeste.Da("AMPLA_CONCORRENCIA", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas)
                with { Descricao = "Vagas sem reserva" },
        }),
    ];

    private static OfertaDeCursoCitada OfertaDeMedicina() => new(
        Guid.CreateVersion7(), "MEDICINA_MARABA", "Medicina", "Bacharelado", "Campus Universitário de Marabá", ["MATUTINO", "VESPERTINO"], null, null, null);

    private static IDescricaoDeCadastro[] DescricoesDoAtendimento()
    {
        ICondicaoAtendimentoReader condicoes = Substitute.For<ICondicaoAtendimentoReader>();
        condicoes.ListarVivosAsync(Arg.Any<CancellationToken>()).Returns([new CondicaoAtendimentoView(Guid.CreateVersion7(), "LACTANTE", "Lactante")]);
        IRecursoAcessibilidadeReader recursos = Substitute.For<IRecursoAcessibilidadeReader>();
        recursos.ListarVivosAsync(Arg.Any<CancellationToken>()).Returns([new RecursoAcessibilidadeView(Guid.CreateVersion7(), "LEDOR", "Ledor")]);
        ITipoDeficienciaReader tipos = Substitute.For<ITipoDeficienciaReader>();
        tipos.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns([new TipoDeficienciaView(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual", "Baixa visão ou cegueira.", true)]);
        return [new DescricaoDeCondicoesDeAtendimento(condicoes), new DescricaoDeRecursosDeAcessibilidade(recursos), new DescricaoDeTiposDeDeficiencia(tipos)];
    }

    private static DescricaoDeBasesLegaisDaFada DescricaoDaFada()
    {
        IBaseLegalDoFadaReader bases = Substitute.For<IBaseLegalDoFadaReader>();
        bases.ObterPorCodigoAsync("LEI_ESTADUAL_EXEMPLO", Arg.Any<CancellationToken>()).Returns(new BaseLegalDoFadaView(
            Guid.CreateVersion7(), "LEI_ESTADUAL_EXEMPLO", "LEI", "Lei estadual de exemplo", "Atenua as desigualdades de acesso na região.",
            [new("1504208", "Marabá", "PA")]));
        return new(bases);
    }
}
