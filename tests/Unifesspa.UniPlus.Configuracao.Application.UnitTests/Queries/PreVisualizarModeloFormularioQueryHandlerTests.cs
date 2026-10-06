namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A pré-visualização avalia o modelo contra respostas simuladas pelo avaliador da inscrição
/// (UNI-REQ-0145): exibição e obrigatoriedade de itens e termos, restrições violadas, pressupostos
/// da inscrição e derivados por regra do catálogo.
/// </summary>
public sealed class PreVisualizarModeloFormularioQueryHandlerTests
{
    private const string Finalidade = "Verificação dos requisitos do processo seletivo.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    private static readonly EtapaDoModelo Dados = new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null);
    private static readonly EtapaDoModelo Revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    private readonly IModeloFormularioRepository _repository = Substitute.For<IModeloFormularioRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly FatoCandidato _certificado = Declarado("CERTIFICADO");

    public PreVisualizarModeloFormularioQueryHandlerTests()
    {
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [_certificado]);
    }

    [Fact(DisplayName = "O pressuposto simulado decide a exibição; sem ele, a exibição fica indeterminada")]
    public async Task Handle_PressupostoSimulado_DecideAExibicao()
    {
        ModeloFormulario modelo = Modelo([Item("CERTIFICADO", 0, exibicao: Quando("CONCLUSAO_REGULAR", true))], pressupostos: ["CONCLUSAO_REGULAR"]);

        PreVisualizacaoDoModeloDto com = (await PreVisualizarAsync(modelo, pressupostos: new() { ["CONCLUSAO_REGULAR"] = true }))!;
        PreVisualizacaoDoModeloDto sem = (await PreVisualizarAsync(modelo))!;

        com.Itens.Single().Should().BeEquivalentTo(new { FatoCodigo = "CERTIFICADO", EtapaCodigo = "DADOS", Visivel = "VERDADEIRO", Obrigatorio = "VERDADEIRO" });
        sem.Itens.Single().Visivel.Should().Be("INDETERMINADO");
    }

    [Fact(DisplayName = "O pressuposto em branco é não informado: nem a condição DIFERENTE se cumpre")]
    public async Task Handle_PressupostoEmBranco_TrataComoNaoInformado()
    {
        PredicadoDnf diferente = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("FORMA_CONCLUSAO", Operador.Diferente, JsonSerializer.SerializeToElement("REGULAR")).Value!)]).Value!;
        ModeloFormulario modelo = Modelo([Item("CERTIFICADO", 0, exibicao: diferente)], pressupostos: ["FORMA_CONCLUSAO"]);

        PreVisualizacaoDoModeloDto resultado = (await PreVisualizarAsync(modelo, pressupostos: new() { ["FORMA_CONCLUSAO"] = "  " }))!;

        resultado.Itens.Single().Visivel.Should().Be("FALSO");
    }

    [Fact(DisplayName = "A resposta que viola restrição aparece com o tipo da restrição")]
    public async Task Handle_RespostaForaDaRestricao_DevolveOTipoDaRestricao()
    {
        ItemDoModelo idade = Item("IDADE", 0, TipoRenderizacao.Numero) with { Restricoes = [RestricoesDeValor.Faixa(0, 10).Value!] };

        PreVisualizacaoDoModeloDto resultado = (await PreVisualizarAsync(Modelo([idade]), respostas: new() { ["IDADE"] = 20 }))!;

        resultado.Itens.Single().RestricoesVioladas.Should().Equal("FAIXA_NUMERICA");
    }

    [Fact(DisplayName = "O derivado por regra do catálogo é resolvido com as respostas e decide a exibição")]
    public async Task Handle_DerivadoDoCatalogo_DecideAExibicao()
    {
        FatoCandidato perfil = FatoCandidato.CriarDerivadoDoAdministrador(
            "PERFIL", "Perfil", null, DominioFato.Booleano, "INSCRICAO", EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        perfil.DefinirRegrasPadrao([RegraDerivacao.CriarBooleana(Quando("CERTIFICADO", true))], new CatalogoDeFatos([_certificado, perfil], []))
            .IsSuccess.Should().BeTrue();
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([_certificado, perfil]);
        ModeloFormulario modelo = Modelo(
            [Item("CERTIFICADO", 0), Item("DECLARACAO", 1, exibicao: Quando("PERFIL", true))],
            derivacoes: new(StringComparer.Ordinal) { ["PERFIL"] = ["CERTIFICADO"] });

        PreVisualizacaoDoModeloDto resultado = (await PreVisualizarAsync(modelo, respostas: new() { ["CERTIFICADO"] = true }))!;

        resultado.Itens.Single(static i => i.FatoCodigo == "DECLARACAO").Visivel.Should().Be("VERDADEIRO");
    }

    [Fact(DisplayName = "O termo aparece conforme as respostas")]
    public async Task Handle_TermoCondicionado_SegueAsRespostas()
    {
        TermoDoModelo termo = new("BANCO_CENTRAL", 0, Guid.NewGuid(), Guid.NewGuid(), Quando("CERTIFICADO", true), Obrigatoriedade.Sempre);
        ModeloFormulario modelo = Modelo([Item("CERTIFICADO", 0)], termos: [termo]);

        PreVisualizacaoDoModeloDto resultado = (await PreVisualizarAsync(modelo, respostas: new() { ["CERTIFICADO"] = false }))!;

        resultado.Termos.Single().Should().BeEquivalentTo(new { Codigo = "BANCO_CENTRAL", Visivel = "FALSO" });
    }

    [Fact(DisplayName = "Modelo inexistente não tem pré-visualização")]
    public async Task Handle_ModeloInexistente_DevolveNulo()
    {
        Kernel.Results.Result<PreVisualizacaoDoModeloDto?> resultado = await PreVisualizarModeloFormularioQueryHandler.Handle(
            new PreVisualizarModeloFormularioQuery(Guid.NewGuid(), new(null, null, null)), _repository, _fatos, CancellationToken.None);

        resultado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "A pré-visualização avalia o grupo repetível: a exibição de cada campo em cada ocorrência e a contagem")]
    public async Task Handle_GrupoRepetivel_AvaliaCadaOcorrencia()
    {
        GrupoDoModelo familia = new(
            "COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", 1, 2, null, Obrigatoriedade.Sempre,
            [Subitem("TRABALHA_NO_CAMPO", 0), Subitem("DECLARACAO_RURAL", 1, exibicao: Quando("TRABALHA_NO_CAMPO", true))]);
        ModeloFormulario modelo = Modelo([Item("CERTIFICADO", 0)], grupos: [familia]);

        PreVisualizacaoDoModeloDto resultado = (await PreVisualizarAsync(modelo, grupos: new()
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaRecebida("m1", Json(new() { ["TRABALHA_NO_CAMPO"] = true })),
                new OcorrenciaRecebida("m2", Json(new() { ["TRABALHA_NO_CAMPO"] = false })),
            ],
        }))!;

        GrupoPreVisualizadoDto grupo = resultado.Grupos.Should().ContainSingle().Which;
        grupo.ContagemValida.Should().BeTrue();
        grupo.Ocorrencias.Select(static o => (o.Id, o.Itens.Single(static i => i.FatoCodigo == "DECLARACAO_RURAL").Visivel))
            .Should().Equal(("m1", "VERDADEIRO"), ("m2", "FALSO"));
    }

    [Fact(DisplayName = "Ocorrência simulada sem identidade própria no grupo é recusada com o caminho dela")]
    public async Task Handle_OcorrenciaSemIdentidade_Recusa()
    {
        GrupoDoModelo familia = new(
            "COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", 0, null, null, Obrigatoriedade.Nunca, [Subitem("TRABALHA_NO_CAMPO", 0)]);
        ModeloFormulario modelo = Modelo([Item("CERTIFICADO", 0)], grupos: [familia]);
        _repository.ObterPorIdParaLeituraAsync(modelo.Id, Arg.Any<CancellationToken>()).Returns(modelo);

        Kernel.Results.Result<PreVisualizacaoDoModeloDto?> resultado = await PreVisualizarModeloFormularioQueryHandler.Handle(
            new PreVisualizarModeloFormularioQuery(modelo.Id, new(null, null, null, new Dictionary<string, IReadOnlyList<OcorrenciaRecebida>?>
            {
                ["COMPOSICAO_FAMILIAR"] = [new OcorrenciaRecebida("m1", null), new OcorrenciaRecebida("m1", null)],
            })),
            _repository, _fatos, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ModeloFormulario.OcorrenciaSimuladaInvalida");
    }

    private async Task<PreVisualizacaoDoModeloDto?> PreVisualizarAsync(
        ModeloFormulario modelo,
        Dictionary<string, object>? respostas = null,
        Dictionary<string, object>? pressupostos = null,
        Dictionary<string, IReadOnlyList<OcorrenciaRecebida>?>? grupos = null)
    {
        _repository.ObterPorIdParaLeituraAsync(modelo.Id, Arg.Any<CancellationToken>()).Returns(modelo);
        Kernel.Results.Result<PreVisualizacaoDoModeloDto?> resultado = await PreVisualizarModeloFormularioQueryHandler.Handle(
            new PreVisualizarModeloFormularioQuery(modelo.Id, new(Json(respostas), null, Json(pressupostos), grupos)),
            _repository, _fatos, CancellationToken.None);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value;
    }

    private static Dictionary<string, JsonElement>? Json(Dictionary<string, object>? valores) =>
        valores?.ToDictionary(static v => v.Key, static v => JsonSerializer.SerializeToElement(v.Value), StringComparer.Ordinal);

    private static ModeloFormulario Modelo(
        IReadOnlyList<ItemDoModelo> itens,
        IReadOnlyList<TermoDoModelo>? termos = null,
        IReadOnlyList<string>? pressupostos = null,
        Dictionary<string, IReadOnlyCollection<string>>? derivacoes = null,
        IReadOnlyList<GrupoDoModelo>? grupos = null)
    {
        Kernel.Results.Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação", null, FinalidadeFormulario.Habilitacao, null,
            new ConteudoDoModelo("Habilitação", [Dados, Revisao], itens, termos ?? [], pressupostos ?? [], grupos ?? []),
            derivacoes ?? new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        return modelo.Value!;
    }

    private static ItemDoModelo Item(string fato, int ordem, TipoRenderizacao tipo = TipoRenderizacao.Booleano, PredicadoDnf? exibicao = null) =>
        new(fato, ordem, "DADOS", fato, tipo, null, null, Obrigatoriedade.Sempre, exibicao, [], false);

    private static ItemDoModelo Subitem(string fato, int ordem, PredicadoDnf? exibicao = null) =>
        new(fato, ordem, null, fato, TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, exibicao, [], false);

    private static PredicadoDnf Quando(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    private static FatoCandidato Declarado(string codigo) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
}
