namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que os testes do cronograma de fases declaram: as fases canônicas do cadastro, as entradas de fase
/// do payload, os tipos de ato do catálogo de Publicações e as regras do catálogo de regras.
/// </summary>
internal static class FasesDoCronogramaDeTeste
{
    public const string AtoDaClassificacao = "RESULTADO_CLASSIFICACAO";

    public static FaseCanonicaView FaseCanonicaDoResultadoFinal() =>
        FaseCanonica("RESULTADO_FINAL", "Resultado Final");

    public static FaseCanonicaView FaseCanonicaRecorrivel() =>
        FaseCanonica("RESULTADO_PRELIMINAR", "Resultado preliminar");

    public static FaseCronogramaInput EntradaDaFase(Guid faseCanonicaId) => new(
        Ordem: 1,
        FaseCanonicaId: faseCanonicaId,
        Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        Produtos: [ProdutoDaClassificacao(PapelProdutoFaseCodigo.Definitivo)],
        FaseConcluinteCodigo: null,
        EmiteParecerIndividual: false,
        BancasRequeridas: [],
        RegraRecurso: null);

    public static ProdutoDaFaseInput ProdutoDaClassificacao(string? papel) => new(AtoDaClassificacao, papel);

    public static ProdutoDaFaseInput ProdutoDoComunicado(string? papel) => new("COMUNICADO", papel);

    public static ProdutoDaFaseInput[] ProdutosPreliminarEDefinitivo() =>
    [
        ProdutoDaClassificacao(PapelProdutoFaseCodigo.Preliminar),
        ProdutoDaClassificacao(PapelProdutoFaseCodigo.Definitivo),
    ];

    public static RegraRecursoFaseInput RecursoAncoradoNaClassificacao() => new(
        RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
        RegraVersao: "v1",
        PrazoValor: 48m,
        PrazoUnidade: UnidadePrazo.Horas,
        AtoAncoraCodigo: AtoDaClassificacao,
        SuspensividadePrimeiraInstanciaValor: null,
        SuspensividadePrimeiraInstanciaUnidade: null,
        AdmiteInstanciaSuperior: false,
        SuspensividadeSegundaInstanciaValor: null,
        SuspensividadeSegundaInstanciaUnidade: null);

    public static TipoAtoPublicadoView TipoAtoDaClassificacao() => new(
        AtoDaClassificacao, "Resultado da classificação",
        CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true);

    public static TipoAtoPublicadoView TipoAtoDoComunicado() => new(
        "COMUNICADO", "Comunicado",
        CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false);

    public static RegraCatalogo RegraDePrazoDeRecurso() =>
        RegraDoCatalogo(RegraPrazoRecursoCodigo.AncoradoEmAto, TipoRegra.RegraPrazoRecurso, "Lei 9.784/1999 art. 56");

    public static RegraCatalogo RegraDeBonus() =>
        RegraDoCatalogo("BONUS-MULTIPLICATIVO", TipoRegra.RegraBonus, "base legal");

    private static FaseCanonicaView FaseCanonica(string codigo, string nome) => new(
        Guid.CreateVersion7(), codigo, nome, null,
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ColetaInscricao: false, ColetaSolicitacaoIsencao: false, OrigemData: "NO_EDITAL");

    private static RegraCatalogo RegraDoCatalogo(string codigo, TipoRegra tipo, string baseLegal) =>
        RegraCatalogo.Criar(
            codigo, "v1", tipo,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, baseLegal).Value!;
}
