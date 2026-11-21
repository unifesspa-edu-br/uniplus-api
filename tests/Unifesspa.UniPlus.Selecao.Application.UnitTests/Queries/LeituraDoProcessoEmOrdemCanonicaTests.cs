namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura do processo devolve cada lista na ordem canônica da configuração, a mesma do documento e do registro publicado, para que o
/// índice que uma recusa aponta seja a posição do item no array lido.
/// </summary>
public sealed class LeituraDoProcessoEmOrdemCanonicaTests
{
    private static EtapaProcesso Etapa(string nome, int? ordem)
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

        return EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(nome, CaraterEtapa.Ambas, tipo, ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null,
                RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Ordem", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static RecursoDaEtapa AoAto(Guid produtoId) => RecursoDaEtapa.Criar(
        AncoraDoRecurso.AtoPublicado,
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
        produtoId).Value!;

    [Fact(DisplayName = "A etapa sem ordem declarada sai depois das que têm ordem, e as sem ordem saem pelo nome")]
    public void ProjetarEtapas_EtapasComESemOrdem_SaemNaOrdemCanonica()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([Etapa("Zeta", null), Etapa("Beta", 2), Etapa("Alfa", null), Etapa("Gama", 1)]).IsSuccess.Should().BeTrue();

        ProjecaoDaEtapaDoProcesso.ProjetarEtapas(processo).Select(static e => e.Identificacao.Nome).Should().Equal("Gama", "Beta", "Alfa", "Zeta");
    }

    [Fact(DisplayName = "Os produtos, as bancas e os recursos da etapa saem na ordem canônica, qualquer que seja a ordem em que foram declarados")]
    public void ProjetarEtapa_ListasDaEtapa_SaemNaOrdemCanonica()
    {
        EtapaProcesso etapa = Etapa("Prova", 1);
        ProdutoDaEtapa resultado = ProdutoDaEtapa.Criar("RESULTADO_DA_PROVA", PapelProdutoFase.Preliminar);
        ProdutoDaEtapa gabarito = ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar);
        etapa.DefinirProdutos([resultado, gabarito]).IsSuccess.Should().BeTrue();
        etapa.DefinirBancas([BancaDaEtapa.Criar(Guid.CreateVersion7(), "ZETA", "Banca Z"), BancaDaEtapa.Criar(Guid.CreateVersion7(), "ALFA", "Banca A")]);
        etapa.DefinirRecursos([AoAto(resultado.Id), AoAto(gabarito.Id)]).IsSuccess.Should().BeTrue();

        EtapaProcessoDto dto = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(etapa);

        dto.Publicacoes.Produtos.Select(static p => p.AtoCodigo).Should().Equal("GABARITO", "RESULTADO_DA_PROVA");
        dto.Bancas.Select(static b => b.Codigo).Should().Equal("ALFA", "ZETA");
        dto.Publicacoes.Recursos.Select(static r => r.ProdutoAncoraId).Should().Equal(gabarito.Id, resultado.Id);
    }
}
