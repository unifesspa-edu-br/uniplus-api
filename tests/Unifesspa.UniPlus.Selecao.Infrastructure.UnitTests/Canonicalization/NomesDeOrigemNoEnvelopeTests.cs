namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class NomesDeOrigemNoEnvelopeTests
{
    [Fact]
    public void Fase_NomeDecomposto_GravaEmNfcERestaura()
    {
        JsonObject fase = SerializarFase("Selec\u0327a\u0303o");
        LeitorEnvelope leitor = new();
        IReadOnlyList<FaseCronograma> lidas = LeituraDoCronograma.LerCronogramaFases(leitor, DocumentoDaFase(fase));

        leitor.Falhou.Should().BeFalse();
        fase["tipoFaseNome"]!.GetValue<string>().Should().Be("Seleção");
        lidas.Single().TipoFaseNome.Should().Be("Seleção");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Fase_NomeAusenteOuVazio_Recusa(string? nome)
    {
        JsonObject fase = SerializarFase("Seleção");
        fase["tipoFaseNome"] = nome;
        if (nome is null)
        {
            fase.Remove("tipoFaseNome");
        }
        LeitorEnvelope leitor = new();

        LeituraDoCronograma.LerCronogramaFases(leitor, DocumentoDaFase(fase));

        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void Fase_NomeAcimaDoLimite_Recusa()
    {
        JsonObject fase = SerializarFase(new string('a', FaseCronograma.TipoFaseNomeMaxLength + 1));
        LeitorEnvelope leitor = new();
        LeituraDoCronograma.LerCronogramaFases(leitor, DocumentoDaFase(fase));
        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void ModeloDeOrigem_NomeDecomposto_GravaEmNfcERestaura()
    {
        ProcessoSeletivo processo = ExemploDeConfiguracao.Entrada().Processo;
        CopiaDeModeloDeFormulario copia = CopiaDoModelo("Formula\u0301rio");
        processo.AplicarModeloDeFormulario(copia).IsSuccess.Should().BeTrue();
        JsonObject documento = JsonNode.Parse(new JsonObject { ["formularios"] = SerializacaoDosFormularios.SerializarFormularios(processo) }.ToJsonString())!.AsObject();
        LeitorEnvelope leitor = new();

        (IReadOnlyList<FormularioProcesso> formularios, _) = LeituraDosFormularios.LerFormularios(leitor, documento);

        leitor.Falhou.Should().BeFalse();
        documento["formularios"]![0]!["modeloOrigem"]!["nome"]!.GetValue<string>().Should().Be("Formulário");
        formularios.Single().ModeloOrigemNome.Should().Be("Formulário");
        formularios.Single().ModeloOrigemCodigo.Should().Be("MODELO");
    }

    [Fact]
    public void EnvelopeCompleto_NomesDaFaseEDoModelo_Restaurados()
    {
        EntradaCanonicalizacao entrada = EntradaComModelo();
        SnapshotCanonico documento = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        DocumentoCongelado congelado = new(documento.Bytes, documento.SchemaVersion, documento.AlgoritmoHash, entrada.HashDocumento);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(entrada.Processo.Id, congelado, Guid.NewGuid(), "usuario-ficticio", DateTimeOffset.UnixEpoch);

        Result<EnvelopeReidratado> resultado = new EnvelopeCodec().Reidratar(versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Configuracao.Cronograma.CronogramaFases.Single().TipoFaseNome.Should().Be("Resultado final");
        resultado.Value.Configuracao.Coleta.Formularios.Single().ModeloOrigemNome.Should().Be("Formulário");
    }

    private static EntradaCanonicalizacao EntradaComModelo()
    {
        EntradaCanonicalizacao entrada = ExemploDeConfiguracao.Entrada();
        entrada.Processo.AplicarModeloDeFormulario(CopiaDoModelo("Formulário")).IsSuccess.Should().BeTrue();
        entrada.Processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(false, null, null).Value!).IsSuccess.Should().BeTrue();
        TipoDeAtoCongelado ato = new("RESULTADO_CLASSIFICACAO", "Resultado da classificação", true, true, false);
        return entrada with { DadosDeCatalogo = new(TiposDeAtoCongelados: new Dictionary<string, TipoDeAtoCongelado> { [ato.Codigo] = ato }) };
    }

    [Fact]
    public void ModeloDeOrigem_Nulo_RestauraAusencia()
    {
        LeitorEnvelope leitor = new();
        (Guid? id, string? codigo, string? nome) = LeituraDosFormularios.LerModeloDeOrigem(
            leitor, new JsonObject { ["modeloOrigem"] = null }, "formulario");

        leitor.Falhou.Should().BeFalse();
        id.Should().BeNull();
        codigo.Should().BeNull();
        nome.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("MODELO", "Modelo", false)]
    [InlineData(null, "Modelo", true)]
    [InlineData("MODELO", null, true)]
    [InlineData("MODELO", "", true)]
    [InlineData("MODELO", "  ", true)]
    public void ModeloDeOrigem_CodigoENome_ExigeCoerencia(string? codigo, string? nome, bool falha)
    {
        JsonObject modelo = new() { ["id"] = Guid.NewGuid().ToString(), ["codigo"] = codigo, ["nome"] = nome };
        LeitorEnvelope leitor = new();
        LeituraDosFormularios.LerModeloDeOrigem(leitor, new JsonObject { ["modeloOrigem"] = modelo }, "formulario");
        leitor.Falhou.Should().Be(falha);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModeloDeOrigem_NomeAusenteOuAcimaDoLimite_Recusa(bool ausente)
    {
        JsonObject modelo = new() { ["id"] = Guid.NewGuid().ToString(), ["codigo"] = "MODELO", ["nome"] = new string('a', FormularioProcesso.ModeloOrigemNomeMaxLength + 1) };
        if (ausente)
        {
            modelo.Remove("nome");
        }
        LeitorEnvelope leitor = new();
        LeituraDosFormularios.LerModeloDeOrigem(leitor, new JsonObject { ["modeloOrigem"] = modelo }, "formulario");
        leitor.Falhou.Should().BeTrue();
    }

    private static JsonObject SerializarFase(string nome)
    {
        OrigemCanonicaDaFase origem = OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(Guid.NewGuid(), "SELECAO", OrigemDataFase.NoEdital, false, false, false, false)) with { Nome = nome };
        FaseCronograma fase = FaseCronograma.Reidratar(ChaveLocal.Criar("fase_selecao").Value!, Guid.NewGuid(), origem,
            new DecisoesDeCronogramaDaFase(1, null, null, null), new PublicacoesDaFase([], false, null), []);
        return JsonNode.Parse(SerializacaoDoCronograma.SerializarFase(fase).ToJsonString())!.AsObject();
    }

    private static JsonObject DocumentoDaFase(JsonObject fase) => new()
    {
        ["cronogramaFases"] = new JsonObject { ["origemCandidatos"] = OrigemCandidatos.ImportacaoExterna.Codigo(), ["fases"] = new JsonArray(fase) },
    };

    private static CopiaDeModeloDeFormulario CopiaDoModelo(string nome) => new(
        FinalidadeFormulario.Habilitacao, "Formulário", [
            PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
            PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão", null, null).Value!,
        ], [], [], [], [], Guid.NewGuid(), "MODELO")
    { ModeloNome = nome };
}
