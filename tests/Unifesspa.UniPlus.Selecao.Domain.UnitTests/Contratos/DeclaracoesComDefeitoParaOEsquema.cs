namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As fases e as etapas declaradas com defeito em cada campo que a conferência localiza, para os assuntos lerem o conjunto como
/// ele chegou: cada declaração recusada leva a causa ao campo dela, e o conjunto recusa a repetição, a ausência e o vínculo.
/// </summary>
internal static class DeclaracoesComDefeitoParaOEsquema
{
    private const TipoDeBanca Banca = TipoDeBanca.AnaliseDocumental;

    internal static ContextoDaConferencia ContextoComTodosOsDefeitos()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        return new ContextoDaConferencia(processo.Declaracao(), CadastrosDeTeste.Para(processo), new CronogramaDoProcesso(Fases()), new EtapasDoProcesso(Etapas()));
    }

    private static FaseDeclarada[] Fases()
    {
        Guid origemRepetida = Guid.CreateVersion7();
        ModeloDeFase invertida = new ModeloDeFase("A", 1) { OrigemId = origemRepetida, ComUnidade = false, AgrupaEtapas = true, EmiteParecerIndividual = true }
            .Janela(5, 1).Publicando(ModeloDeFase.Preliminar("X"), ModeloDeFase.Preliminar("X"));

        return
        [
            (invertida with { Bancas = [BancaDeFase("R", "R"), BancaDeFase()] }).Declarar(),
            new ModeloDeFase("A", 1) { OrigemId = origemRepetida, ColetaInscricao = true }.Declarar(),
            new ModeloDeFase("I", 3) { ColetaSolicitacaoIsencao = true, Bancas = [BancaDeFase(), BancaDeFase()] }.Declarar(),
            new ModeloDeFase("P", 4) { EmiteParecerIndividual = true }.Declarar(),
        ];
    }

    private static BancaRequerida BancaDeFase(params string[] categorias) => BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
        TiposDeBancaCopiadosDeTeste.De(Banca), [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    private static EtapaDeclarada[] Etapas()
    {
        Guid repetido = Guid.CreateVersion7();
        DateTimeOffset inicio = ModeloDeFase.Abertura;

        return
        [
            (new ModeloDeEtapa(string.Empty) { Id = repetido, Ordem = 1, Peso = null, NotaMaxima = null, Forma = null }).Declarar(),
            (new ModeloDeEtapa("Prova") { Id = repetido, Ordem = 1, FaseCodigo = "FASE_QUE_NAO_EXISTE", Inicio = inicio.AddDays(2), Fim = inicio }).Declarar(),
            (new ModeloDeEtapa("Prova do ENEM") { Ordem = 2 }.DoEnem() with { Peso = null, Bancas = [BancasDeTeste.DeRecursosDaEtapa()] }).Declarar(),
            new ModeloDeEtapa("Outra do ENEM") { Ordem = 3 }.DoEnem().Declarar(),
            (new ModeloDeEtapa("ENEM com conceito") { Ordem = 4 }.DoEnem() with { Conceito = ModeloDeEtapa.AptoInapto() }).Declarar(),
            (new ModeloDeEtapa("Escala fora do passo") { Ordem = 5 } with { NotaMinima = 11m, NotaMaxima = 10m, Passo = 0m }).Declarar(),
            (new ModeloDeEtapa("Eliminatória com peso") { Ordem = 6 }.SemNota() with { Peso = 1m, EmiteParecerIndividual = true }).Declarar(),
        ];
    }
}
