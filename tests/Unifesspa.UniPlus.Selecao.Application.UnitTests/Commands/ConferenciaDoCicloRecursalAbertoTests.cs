namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A retificação não desfaz o ciclo de recurso que o preliminar publicado abriu: enquanto o definitivo da
/// matéria não saiu, o preliminar e o definitivo continuam no cronograma, ainda fechando o mesmo ciclo.
/// </summary>
public sealed class ConferenciaDoCicloRecursalAbertoTests
{
    private const string Materia = "RESULTADO_PROVA";
    private const string NomeDaMateria = "Resultado da prova";
    private static readonly DateTimeOffset Agora = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly List<AtoDoProcesso> _atos = [];

    [Fact(DisplayName = "Preliminar publicado e retificação que retira o definitivo da fase é recusada, nomeando a matéria")]
    public void PreliminarPublicado_RetiraODefinitivo_ERecusada()
    {
        ProdutoDaFase preliminar = Produto(PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = Produto(PapelProdutoFase.Definitivo);
        Publicar(preliminar);

        Result resultado = Conferir(
            publicada: [Fase("PROVA", 1, null, preliminar, definitivo)],
            nova: [Fase("PROVA", 1, null, preliminar)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
        resultado.Error.Message.Should().Contain(NomeDaMateria).And.Contain("Publique o definitivo antes de retificar");
    }

    [Fact(DisplayName = "Preliminar publicado e retificação que leva o definitivo para outro ciclo é recusada")]
    public void PreliminarPublicado_MudaODefinitivoDeCiclo_ERecusada()
    {
        ProdutoDaFase preliminar = Produto(PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = Produto(PapelProdutoFase.Definitivo);
        ProdutoDaFase definitivoDeOutraFase = Produto(PapelProdutoFase.Definitivo);
        Publicar(preliminar);

        Result resultado = Conferir(
            publicada: [Fase("PROVA", 1, "RESULTADO", preliminar), Fase("RESULTADO", 2, null, definitivo), Fase("RECURSOS", 3, null)],
            nova: [Fase("PROVA", 1, "RECURSOS", preliminar), Fase("RESULTADO", 2, null), Fase("RECURSOS", 3, null, definitivoDeOutraFase)]);

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
    }

    [Fact(DisplayName = "Com o definitivo já publicado a retificação que retira os dois itens é aceita")]
    public void DefinitivoPublicado_RetiraOsItens_EAceita()
    {
        ProdutoDaFase preliminar = Produto(PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = Produto(PapelProdutoFase.Definitivo);
        Publicar(preliminar);
        Publicar(definitivo);

        Result resultado = Conferir(
            publicada: [Fase("PROVA", 1, null, preliminar, definitivo)],
            nova: [Fase("PROVA", 1, null)]);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Com o preliminar não publicado a retificação que retira o par é aceita")]
    public void PreliminarNaoPublicado_RetiraOPar_EAceita()
    {
        ProdutoDaFase preliminar = Produto(PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = Produto(PapelProdutoFase.Definitivo);

        Result resultado = Conferir(
            publicada: [Fase("PROVA", 1, null, preliminar, definitivo)],
            nova: [Fase("PROVA", 1, null)]);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Preliminar publicado e retificação que mantém o par no mesmo ciclo é aceita")]
    public void PreliminarPublicado_MantemOPar_EAceita()
    {
        ProdutoDaFase preliminar = Produto(PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = Produto(PapelProdutoFase.Definitivo);
        Publicar(preliminar);

        Result resultado = Conferir(
            publicada: [Fase("PROVA", 1, null, preliminar, definitivo)],
            nova: [Fase("PROVA", 1, null, preliminar, definitivo)]);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Preliminar publicado e cronograma novo com os mesmos itens, de identificadores diferentes, é aceita")]
    public void PreliminarPublicado_ItensComIdentificadoresNovosNoMesmoLugar_EAceita()
    {
        ProdutoDaFase preliminar = Produto(PapelProdutoFase.Preliminar);
        Publicar(preliminar);

        Result resultado = Conferir(
            publicada: [Fase("PROVA", 1, null, preliminar, Produto(PapelProdutoFase.Definitivo))],
            nova: [Fase("PROVA", 2, null, Produto(PapelProdutoFase.Preliminar), Produto(PapelProdutoFase.Definitivo))]);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Preliminar publicado numa etapa renomeada, com o mesmo par, é aceita")]
    public void PreliminarPublicado_EtapaRenomeada_EAceita()
    {
        Guid etapaId = Guid.CreateVersion7();
        ProdutoDaEtapa preliminar = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        Publicar(preliminar.Id);

        Result resultado = ConferenciaDoCicloRecursalAberto.Conferir(
            [], [Etapa(etapaId, "Prova objetiva", preliminar, ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo))],
            [], [Etapa(etapaId, "Prova objetiva (reaplicação)", ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo))],
            CadeiasDosProdutosPrevistos.De(_atos));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Preliminar publicado e retirada do par de uma de duas etapas de mesmo nome é recusada")]
    public void PreliminarPublicado_RetiraOParDeUmaDeDuasEtapasHomonimas_ERecusada()
    {
        Guid primeira = Guid.CreateVersion7();
        Guid segunda = Guid.CreateVersion7();
        ProdutoDaEtapa preliminar = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        Publicar(preliminar.Id);

        Result resultado = ConferenciaDoCicloRecursalAberto.Conferir(
            [],
            [
                Etapa(primeira, "Prova", preliminar, ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo)),
                Etapa(segunda, "Prova", ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo)),
            ],
            [],
            [
                Etapa(primeira, "Prova"),
                Etapa(segunda, "Prova", ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo)),
            ],
            CadeiasDosProdutosPrevistos.De(_atos));

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
    }

    private Result Conferir(FaseCronograma[] publicada, FaseCronograma[] nova) =>
        ConferenciaDoCicloRecursalAberto.Conferir(publicada, [], nova, [], CadeiasDosProdutosPrevistos.De(_atos));

    private static ProdutoDaFase Produto(PapelProdutoFase papel) => ProdutoDaFase.Criar(Materia, papel);

    private static FaseCronograma Fase(string codigo, int ordem, string? faseConcluinteCodigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: Agora, fim: Agora.AddDays(10), produtos: produtos,
            faseConcluinteCodigo: faseConcluinteCodigo, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private void Publicar(ProdutoDaFase produto) => Publicar(produto.Id);

    private static EtapaProcesso Etapa(Guid id, string nome, params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            nome, CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe("PROVA_OBJETIVA"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 1m, ordem: 1).Value!;
        typeof(EtapaProcesso).GetProperty(nameof(EtapaProcesso.Id))!.SetValue(etapa, id);
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private void Publicar(Guid produtoId)
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), _processoId, Materia, NomeDaMateria, "1/2027", new DateOnly(2027, 1, 20), "Diretor do CEPS", "ceps", "psm-medicina-2027",
                Guid.CreateVersion7(), [], ProdutoDoEditalId: produtoId),
            new RelogioFixo(Agora)).Value!;
        ato.ConfirmarRegistro(new RelogioFixo(Agora));
        _atos.Add(ato);
    }
}
