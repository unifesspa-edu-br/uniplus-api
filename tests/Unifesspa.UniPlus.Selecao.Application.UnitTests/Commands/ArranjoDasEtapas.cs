namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDasEtapas;

/// <summary>
/// Os processos, os pedidos de gravação e os cadastros que os casos da edição das etapas compõem. Cada peça
/// descreve uma coisa do domínio — a etapa que o agregado já tem, a que o payload traz, o ato que o catálogo
/// conhece — para que o caso diga só o que o distingue dos demais.
/// </summary>
internal static class ArranjoDasEtapas
{
    public const string AtoDaClassificacao = "RESULTADO_CLASSIFICACAO";
    public const string RegraDoPrazoAncoradoEmAto = "RECURSO-PRAZO-ANCORADO-EM-ATO";

    /// <summary>Certame recém-criado, ainda sem etapa alguma.</summary>
    public static ProcessoSeletivo ProcessoSemEtapas() => ProcessoSeletivo.Criar(
        "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    public static ProcessoSeletivo ProcessoCom(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        // Falha aqui é fixture inválida, não cenário: sem a asserção o processo voltaria sem
        // etapa nenhuma e o teste morreria depois, longe da causa.
        processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue("a etapa da fixture tem de ser aceita pelo agregado");
        return processo;
    }

    public static TipoEtapaSnapshot TipoEtapaProvaObjetiva() => SnapshotDoTipo(
        TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true);

    public static TipoEtapaSnapshot SnapshotDoTipo(
        Guid origemId, string codigo, string nome, bool admitePontuacao, bool admiteEliminacao, bool notaDeOrigemNoEnem = false) =>
        TipoEtapaSnapshot.Criar(origemId, codigo, nome, admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem).Value!;

    public static EtapaProcesso EtapaClassificatoriaDoAgregado(string nome, int ordem, TipoEtapaSnapshot tipo) =>
        EtapaProcesso.Criar(nome, CaraterEtapa.Classificatoria, tipo, peso: 1m, ordem: ordem).Value!;

    public static EtapaProcesso EtapaDaProvaObjetiva(string nome = "Prova Objetiva") =>
        EtapaClassificatoriaDoAgregado(nome, 1, TipoEtapaProvaObjetiva());

    public static EtapaProcessoInput EtapaClassificatoria(string nome, Guid tipoOrigemId, int ordem = 1, Guid? id = null) => new(
        nome, CaraterEtapa.Classificatoria, tipoOrigemId, 1m, null, ordem, id, Produtos: [], Bancas: [], Recursos: []);

    public static EtapaProcessoInput EtapaEliminatoria(
        string nome, Guid tipoOrigemId, int ordem, decimal notaMinima = 5m, Guid? id = null) => new(
        nome, CaraterEtapa.Eliminatoria, tipoOrigemId, null, notaMinima, ordem, id, Produtos: [], Bancas: [], Recursos: []);

    public static EtapaProcessoInput ProvaObjetiva(string nome = "Prova Objetiva", int ordem = 1, Guid? id = null) =>
        EtapaClassificatoria(nome, TipoProvaObjetivaOrigemId, ordem, id);

    /// <summary>
    /// Prova que publica o resultado da classificação, preliminar e definitivo, e que ancora a janela recursal
    /// no ato preliminar quando <paramref name="comAncora"/>.
    /// </summary>
    public static EtapaProcessoInput ProvaPublicandoAClassificacao(
        string nome = "Prova Objetiva", Guid? id = null, bool comAncora = false, bool admiteInstanciaSuperior = false) =>
        ProvaObjetiva(nome, id: id) with
        {
            Produtos = PreliminarEDefinitivo(AtoDaClassificacao),
            Recursos = comAncora ? [RecursoAncoradoNoAto(AtoDaClassificacao, admiteInstanciaSuperior)] : [],
        };

    public static ProdutoDaEtapaInput Preliminar(string ato) => new(ato, "PRELIMINAR");

    public static ProdutoDaEtapaInput Definitivo(string ato) => new(ato, "DEFINITIVO");

    public static IReadOnlyList<ProdutoDaEtapaInput> PreliminarEDefinitivo(string ato) => [Preliminar(ato), Definitivo(ato)];

    public static RecursoDaEtapaInput RecursoAncoradoNoAto(string ato, bool admiteInstanciaSuperior = false) => new(
        AncoraDoRecurso.AtoPublicado, RegraDoPrazoAncoradoEmAto, "v1",
        2m, UnidadePrazo.DiasUteis, ato, null, null, admiteInstanciaSuperior, null, null);

    public static DefinirEtapasCommand Definindo(ProcessoSeletivo processo, params EtapaProcessoInput[] etapas) =>
        new(processo.Id, etapas, PrecondicaoIfMatch.Ausente);

    public static DefinirEtapasCommand Redefinindo(ProcessoSeletivo processo, params EtapaProcessoInput[] etapas) =>
        new(processo.Id, etapas, PrecondicaoIfMatch.Curinga);

    public static TipoEtapaView TipoDoCadastro(
        Guid origemId, string codigo, string nome, bool admitePontuacao = true, bool admiteEliminacao = true, bool notaDeOrigemNoEnem = false) =>
        new(origemId, codigo, nome, null, admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem);

    /// <summary>O cadastro de tipos de etapa que resolve, entre os ativos, só os tipos dados.</summary>
    public static ITipoEtapaReader CadastroDeTipos(params TipoEtapaView[] tipos)
    {
        ITipoEtapaReader cadastro = Substitute.For<ITipoEtapaReader>();
        foreach (TipoEtapaView tipo in tipos)
        {
            cadastro.ObterAtivoPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        }

        return cadastro;
    }

    /// <summary>O cadastro em que todo tipo de etapa está inexistente ou inativo.</summary>
    public static ITipoEtapaReader CadastroSemTipoAtivo()
    {
        ITipoEtapaReader cadastro = Substitute.For<ITipoEtapaReader>();
        cadastro.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        return cadastro;
    }

    /// <summary>Entrada de catálogo de um tipo de ato que divulga resultado.</summary>
    public static TipoAtoPublicadoView AtoDeResultado(string codigo) => new(
        codigo, $"Ato de {codigo}", CongelaConfiguracao: false, UnicoPorObjeto: false,
        EfeitoIrreversivel: false, EhResultado: true);

    public static ITipoAtoPublicadoReader CatalogoCom(TipoAtoPublicadoView ato)
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(ato.Codigo, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(ato);
        return catalogo;
    }

    public static ITipoAtoPublicadoReader CatalogoDoResultado(string codigo) => CatalogoCom(AtoDeResultado(codigo));

    /// <summary>O catálogo que não conhece código algum de ato.</summary>
    public static ITipoAtoPublicadoReader CatalogoSemAtos()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        return catalogo;
    }

    public static RegraCatalogo RegraDoCatalogo(string codigo, TipoRegra tipo) => RegraCatalogo.Criar(
        codigo, "v1", tipo, JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
        "Decisão institucional da Unifesspa").Value!;

    public static IRegraCatalogoReader CatalogoDeRegras(RegraCatalogo regra)
    {
        IRegraCatalogoReader catalogo = Substitute.For<IRegraCatalogoReader>();
        catalogo.ObterAsync(regra.Codigo, regra.Versao, Arg.Any<CancellationToken>()).Returns(regra);
        return catalogo;
    }

    public static IRegraCatalogoReader CatalogoDoPrazoAncoradoEmAto() => CatalogoDeRegras(RegraCatalogo.Criar(
        RegraDoPrazoAncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
        JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
        "Lei 9.784/1999, art. 59").Value!);
}
