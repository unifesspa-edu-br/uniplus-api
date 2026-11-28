namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O quadro de vagas que a exportação do rascunho traz é o mesmo que a serialização do envelope projeta para a mesma
/// distribuição, ainda que as modalidades venham fora da ordem do código.
/// </summary>
public sealed class QuadroDeVagasDoRascunhoIgualAoSerializadoTests
{
    private const string Oferta = "MEDICINA_MARABA";
    private const string Ampla = "AC";
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();
    private static readonly DateTimeOffset LidoEm = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    // O código da modalidade fora da ordem ordinal, com número dentro do código: "L10" vem antes de "L2" na ordem ordinal.
    private static readonly (string Codigo, int Quantidade)[] ModalidadesForaDeOrdem = [("L2", 2), ("AC_PCD", 1), ("L10", 1), (Ampla, 5), ("L1", 1)];

    private static string CodigoDaRegra => RegraDeDistribuicaoDeVagas.Institucional.Codigo();

    [Fact(DisplayName = "O quadro e o total da exportação do rascunho são os da serialização do envelope, na mesma ordem de modalidades")]
    public async Task Exportacao_MesmaDistribuicao_TrazOMesmoQuadroDaSerializacao()
    {
        JsonObject exportado = await ExportarAsync();

        JsonObject serializado = SerializacaoDoQuadroDeVagas.SerializarVagasDaOferta(DistribuicaoCalculada());
        JsonObject item = exportado["distribuicaoVagas"]![0]!.AsObject();
        JsonNode.DeepEquals(item["quadro"], serializado["quadro"]).Should().BeTrue("a exportação e o envelope descrevem o quadro do mesmo jeito");
        item["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(serializado["totalDeVagasDoEdital"]!.GetValue<int>());
        item["quadro"]!.AsArray().Select(static linha => linha!["modalidadeCodigo"]!.GetValue<string>())
            .Should().Equal("AC", "AC_PCD", "L1", "L10", "L2");
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoCalculada() =>
        VagasLidas.Ler(Declaracoes(), Cadastros()).Vagas.DistribuicaoVagas.Should().ContainSingle().Subject;

    private static DeclaracoesDoDocumento Declaracoes()
    {
        ModalidadeNoDocumento[] modalidades = [.. ModalidadesForaDeOrdem.Select(static (modalidade, posicao) => new ModalidadeNoDocumento(posicao, modalidade.Codigo, modalidade.Quantidade))];
        DistribuicaoNoDocumento distribuicao = new(0, Oferta, 10, modalidades) { RegraDeDistribuicao = new(CodigoDaRegra, "v1") };

        return new(new IdentificacaoNoDocumento(null, null, null, null, null, null, null), []) { Vagas = new(null, [distribuicao]) };
    }

    private static CadastrosCitadosNoDocumento Cadastros() => new(LidoEm)
    {
        Regras = new Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>
        {
            [(CodigoDaRegra, "v1")] = new(TipoRegra.RegraDistribuicaoVagas, ReferenciaRegra.De(CodigoDaRegra, "v1", Hash), JsonDocument.Parse("{}").RootElement.Clone()),
        },
        VagasEExigencias = new()
        {
            OfertasDeCurso = new Dictionary<string, OfertaDeCursoCitada> { [Oferta] = new(Guid.CreateVersion7(), Oferta, "Medicina", "Bacharelado", "Campus Universitário de Marabá", ["MATUTINO"], null, null, null) },
            Modalidades = ModalidadesForaDeOrdem.ToDictionary(static modalidade => modalidade.Codigo, static modalidade => ModalidadeNoCadastro(modalidade.Codigo), StringComparer.Ordinal),
        },
    };

    private static ModalidadeNoCadastro ModalidadeNoCadastro(string codigo) => codigo == Ampla
        ? ModalidadesNoCadastroDeTeste.Da(codigo, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas)
        : ModalidadesNoCadastroDeTeste.Da(codigo, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade) with { ComposicaoOrigemCodigo = Ampla };

    private static async Task<JsonObject> ExportarAsync()
    {
        ExportacaoDoRascunho exportacao = new(
            Substitute.For<IUnidadeReader>(),
            [],
            LeituraDasVagas(),
            new ResolucaoDosModelosCitados(Substitute.For<IModeloDeDocumentoRepository>()),
            new DescricaoDaLocalidade(Substitute.For<IConferenciaEnderecoGeo>()),
            Esquema);

        return await exportacao.ExportarAsync(Rascunho(), CancellationToken.None);
    }

    private static LeituraDasVagasDoRascunho LeituraDasVagas()
    {
        IOfertaCursoReader ofertas = Substitute.For<IOfertaCursoReader>();
        ofertas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs([OfertaVista()]);
        IModalidadeReader modalidades = Substitute.For<IModalidadeReader>();
        modalidades.ListarVivosPorCodigosAsync(default!, default).ReturnsForAnyArgs([.. ModalidadesForaDeOrdem.Select(static modalidade => ModalidadeVista(modalidade.Codigo))]);
        IRegraCatalogoReader regras = Substitute.For<IRegraCatalogoReader>();
        regras.ListarPorReferenciasAsync(default!, default).ReturnsForAnyArgs([RegraDoCatalogo()]);

        return LeituraSobre(ofertas, modalidades, regras);
    }

    private static LeituraDasVagasDoRascunho LeituraSobre(IOfertaCursoReader ofertas, IModalidadeReader modalidades, IRegraCatalogoReader regras) => new(
        new LeituraDosCadastrosDasVagasCitados(
            ofertas,
            modalidades,
            Substitute.For<IReferenciaReservaDemograficaReader>(),
            Substitute.For<ICondicaoAtendimentoReader>(),
            Substitute.For<IRecursoAcessibilidadeReader>(),
            Substitute.For<ITipoDeficienciaReader>(),
            Substitute.For<IBaseLegalDoFadaReader>()),
        new LeituraDosCadastrosDoCatalogoCitados(regras, Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<IFatoCandidatoReader>()),
        new FakeTimeProvider(LidoEm));

    private static RascunhoDeProcessoSeletivo Rascunho()
    {
        string modalidades = string.Join(", ", ModalidadesForaDeOrdem.Select(static modalidade => $$"""{ "codigo": "{{modalidade.Codigo}}", "quantidadeDeclarada": {{modalidade.Quantidade}} }"""));
        string documento = $$"""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "distribuicaoVagas": [ { "ofertaCursoCodigo": "{{Oferta}}", "vagasOfertadasBase": 10,
                "regraDistribuicao": { "codigo": "{{CodigoDaRegra}}", "versao": "v1" }, "modalidades": [ {{modalidades}} ] } ] }
            """;
        return RascunhoDeProcessoSeletivo.Criar(Guid.CreateVersion7(), DocumentoDoRascunho.Criar(documento, null).Value!).Value!;
    }

    private static OfertaCursoView OfertaVista() => new(
        Guid.CreateVersion7(), Oferta, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "CAMAR", "Campus Universitário de Marabá", "CAMPUS",
        "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"], null, null, null, null, null, null, "Medicina", "Bacharelado");

    private static ModalidadeView ModalidadeVista(string codigo)
    {
        ModalidadeNoCadastro cadastro = ModalidadeNoCadastro(codigo);
        return new(
            cadastro.Id, codigo, cadastro.Nome, null, cadastro.NaturezaLegal!.Value.Codigo(), cadastro.ComposicaoVagas!.Value.Codigo(),
            cadastro.ComposicaoOrigemCodigo, null, null, null, null, cadastro.RegraDeAplicabilidade, null, cadastro.BaseLegal);
    }

    private static RegraCatalogo RegraDoCatalogo() =>
        RegraCatalogo.Criar(CodigoDaRegra, "v1", TipoRegra.RegraDistribuicaoVagas, JsonDocument.Parse("{}").RootElement.Clone(), JsonDocument.Parse("[]").RootElement.Clone(), "Regra de teste");
}
