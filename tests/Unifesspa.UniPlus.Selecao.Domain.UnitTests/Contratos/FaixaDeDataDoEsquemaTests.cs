namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Json.Schema;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class FaixaDeDataDoEsquemaTests
{
    private static readonly JsonSchema Esquema = JsonSchema.FromText(File.ReadAllText(
        Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1", "restricao-de-valor.schema.json")));

    [Fact]
    public async Task ExportarAsync_FormularioBasico_RestricoesDeDataAtendemAoEsquema()
    {
        ProcessoSeletivo processo = CriarProcesso();
        JsonObject documento = await ExportarFormulario(processo);
        JsonArray dados = documento["formularios"]![0]!["itens"]!.AsArray();
        dados.Count.Should().Be(ConjuntoBasicoDaInscricao.Itens.Count);
        List<JsonNode> restricoes = [.. dados.SelectMany(dado => dado!["restricoes"]!.AsArray())
            .Where(restricao => restricao!["tipo"]!.GetValue<string>() == "FAIXA_DE_DATA").Select(restricao => restricao!)];
        restricoes.Should().NotBeEmpty();
        restricoes.Should().Contain(item => item["dataMinima"]!["anos"]!.GetValue<int>() == -120);
        restricoes.Should().OnlyContain(item => Esquema.Evaluate(JsonSerializer.SerializeToElement(item)).IsValid);
    }

    private static async Task<JsonObject> ExportarFormulario(ProcessoSeletivo processo)
    {
        JsonObject documento = DocumentoDoFormulario(processo);
        DocumentoDoRascunho conteudo = DocumentoDoRascunho.Criar(documento.ToJsonString(), null).Value!;
        RascunhoDeProcessoSeletivo rascunho = RascunhoDeProcessoSeletivo.Criar(Guid.NewGuid(), conteudo).Value!;
        IUnidadeReader unidades = Substitute.For<IUnidadeReader>();
        DescricaoDoDocumentoDoRascunho descricao = new(
            new EsquemaDoDocumentoDaConfiguracao(),
            [new DescricaoDeUnidades(unidades), new DescricaoDeTiposDeProcesso(Substitute.For<ITipoProcessoReader>())]);
        ExportacaoDoRascunho exportacao = new(unidades, descricao, new ResolucaoDosModelosCitados(Substitute.For<IModeloDeDocumentoRepository>()));
        return await exportacao.ExportarAsync(rascunho, CancellationToken.None);
    }

    private static JsonObject DocumentoDoFormulario(ProcessoSeletivo processo)
    {
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(Entrada(processo));
        JsonArray dados = JsonNode.Parse(congelado.Bytes)!["dadosDosFormularios"]!.AsArray();
        JsonArray itens = new([.. dados.Select(dado => (JsonNode)new JsonObject
        {
            ["codigoDoDado"] = dado!["codigoDoDado"]!.DeepClone(),
            ["restricoes"] = SemContexto(dado["restricoes"]!.DeepClone()),
        })]);
        return new JsonObject
        {
            ["formato"] = "uniplus.selecao.configuracao",
            ["versaoFormato"] = 1,
            ["formularios"] = new JsonArray(new JsonObject { ["finalidade"] = "INSCRICAO", ["itens"] = itens }),
        };
    }

    private static ProcessoSeletivo CriarProcesso()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("Seleção de exemplo", TiposDeProcessoDeTeste.SiSU,
            OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", new string('a', 64)),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", new string('b', 64)),
        }).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, null, null, FormularioDeTeste.Paginas(), FormularioDeTeste.DadosBasicos())
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static EntradaCanonicalizacao Entrada(ProcessoSeletivo processo)
    {
        DadosEdital dados = DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid()).Value!;
        return new(processo, dados, new string('a', 64), FusoInstitucional.ZoneId, null, null, new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados()));
    }

    [Theory]
    [InlineData("anos", -LimiteDeData.TetoDeAnos, true)]
    [InlineData("anos", LimiteDeData.TetoDeAnos, true)]
    [InlineData("anos", -LimiteDeData.TetoDeAnos - 1, false)]
    [InlineData("anos", LimiteDeData.TetoDeAnos + 1, false)]
    [InlineData("dias", -LimiteDeData.TetoDeDias, true)]
    [InlineData("dias", LimiteDeData.TetoDeDias, true)]
    [InlineData("dias", -LimiteDeData.TetoDeDias - 1, false)]
    [InlineData("dias", LimiteDeData.TetoDeDias + 1, false)]
    public void Esquema_DeslocamentoNosLimitesDoDominio_ConfereValor(string unidade, int valor, bool valido)
    {
        JsonObject limite = new() { ["referenciaDaData"] = "ENVIO", [unidade] = valor };
        JsonObject restricao = new() { ["tipo"] = "FAIXA_DE_DATA", ["dataMinima"] = limite };

        Esquema.Evaluate(JsonSerializer.SerializeToElement(restricao)).IsValid.Should().Be(valido);
    }

    // O rascunho é escrito por quem edita e não traz o contexto de rótulos que só o congelado registra.
    private static JsonNode SemContexto(JsonNode no)
    {
        foreach (JsonNode filho in Filhos(no))
        {
            SemContexto(filho);
        }

        (no as JsonObject)?.Remove("contexto");
        return no;
    }

    private static JsonNode[] Filhos(JsonNode no) => no switch
    {
        JsonObject objeto => [.. objeto.Select(static par => par.Value).OfType<JsonNode>()],
        JsonArray lista => [.. lista.OfType<JsonNode>()],
        _ => [],
    };
}
