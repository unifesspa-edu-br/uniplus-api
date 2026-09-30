namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// issue #1077 — §2: defesa em profundidade da fronteira Application. O caminho esperado para
/// um fato de escopo-processo sem oferta é <c>ProcessoSeletivo.PendenciaDeFatoColetadoSemValoresOfertados</c>,
/// avaliado ANTES deste resolvedor rodar — os testes abaixo chamam o resolvedor DIRETO,
/// contornando o gate, para provar que ele também nunca devolve sucesso com lista vazia.
/// </summary>
public sealed class ResolvedorValoresSelecionaveisCongeladosTests
{
    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Resolvedor", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static FatoCandidatoView FatoCategoricoDeEscopoProcesso(string codigo) => new(
        Id: Guid.CreateVersion7(),
        Codigo: codigo,
        Nome: codigo,
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DECLARADO",
        Cardinalidade: "MULTIVALORADO",
        ValoresDominio: null,
        PontoResolucao: "INSCRICAO",
        Binding: $"CAMPO_INSCRICAO:{codigo}",
        ValoresDominioDeclarados: null, FonteValores: "PROCESSO", Ativo: true);

    [Fact(DisplayName = "Resolver congela, para fato de fonte do processo, as opções que o processo declarou, na ordem declarada")]
    public void Resolver_FatoDeFonteDoProcesso_CongelaAsOpcoesDeclaradas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [FatoColetado.Criar("EDICAO_ENEM", 0, "Edição do ENEM", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, origemValores: OrigemValoresColeta.OpcoesDoProcesso).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "EDICAO_ENEM",
            [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2025", "ENEM 2025", 0).Value!,
             OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2024", "ENEM 2024", 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["EDICAO_ENEM"] = FatoCategoricoDeEscopoProcesso("EDICAO_ENEM"),
        };

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["EDICAO_ENEM"];

        valores!.Select(static v => v.Codigo).Should().Equal("2025", "2024");
        valores![0].Descricao.Should().Be("ENEM 2025");
    }

    [Theory(DisplayName = "Resposta de campo que forma as opções de outro precisa ser opção desse outro na publicação")]
    [InlineData(new[] { "MEDICINA" }, true)]
    [InlineData(new[] { "MEDICINA", "DIREITO" }, false)]
    public void Resolver_OpcoesDasRespostas_ExigemAsOpcoesDaFonteNoAlvo(string[] opcoesDaFonte, bool aceita)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
        [
            FatoColetado.Criar("OPCAO_CURSO_1", 0, "1ª opção", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso).Value!,
            FatoColetado.Criar("OPCAO_LISTA_ESPERA", 1, "Lista de espera", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso, restricoes: [new OpcoesDasRespostas(["OPCAO_CURSO_1"])]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("OPCAO_CURSO_1",
            [.. opcoesDaFonte.Select(static (o, i) => OpcaoDeclaradaFato.Criar("OPCAO_CURSO_1", o, o, i).Value!)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("OPCAO_LISTA_ESPERA",
            [OpcaoDeclaradaFato.Criar("OPCAO_LISTA_ESPERA", "MEDICINA", "MEDICINA", 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["OPCAO_CURSO_1"] = FatoCategoricoDeEscopoProcesso("OPCAO_CURSO_1"),
            ["OPCAO_LISTA_ESPERA"] = FatoCategoricoDeEscopoProcesso("OPCAO_LISTA_ESPERA"),
        };

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);

        if (aceita)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.OpcoesDeOutroDominio);
        }
    }

    [Fact(DisplayName = "Resolver congela, para fato de fonte dos municípios do bônus, os municípios da área em ordem de nome")]
    public void Resolver_FatoDosMunicipiosDoBonus_CongelaOsMunicipiosDaArea()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirBonusRegional(ConfiguracaoBonusRegional.Criar(
            ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)).Value!,
            1.20m, null, Guid.NewGuid(), "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional",
            [("1505536", "Parauapebas", "PA"), ("1504208", "Marabá", "PA"), ("1500131", "Água Azul do Norte", "PA")]).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("MUNICIPIO_EM_AREA_BONUS", 0, "Município", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.MunicipiosDoBonus).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["MUNICIPIO_EM_AREA_BONUS"] = FatoCategoricoDeEscopoProcesso("MUNICIPIO_EM_AREA_BONUS") with { FonteValores = "MUNICIPIOS_BONUS" },
        };

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["MUNICIPIO_EM_AREA_BONUS"];

        valores!.Select(static v => (v.Codigo, v.Descricao)).Should().Equal(
            ("1500131", "Água Azul do Norte/PA"), ("1504208", "Marabá/PA"), ("1505536", "Parauapebas/PA"));
    }

    [Fact(DisplayName = "Resolver recusa CONDICAO_ATENDIMENTO coletável sem nenhuma condição ofertada")]
    public void Resolver_CondicaoAtendimentoSemOferta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = FatoCategoricoDeEscopoProcesso("CONDICAO_ATENDIMENTO"),
        };

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "Resolver aceita CONDICAO_ATENDIMENTO coletável com condição ofertada e devolve lista não vazia")]
    public void Resolver_CondicaoAtendimentoComOferta_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")], [], []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = FatoCategoricoDeEscopoProcesso("CONDICAO_ATENDIMENTO"),
        };

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!["CONDICAO_ATENDIMENTO"].Should().ContainSingle(v => v.Codigo == "PCD");
    }
}
