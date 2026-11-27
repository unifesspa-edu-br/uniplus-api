namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os critérios de desempate do documento. O documento não tem <c>ordem</c>: a ordem do critério é a posição dele entre os adotados,
/// a partir de 1, e o critério recusado não ocupa posição. A regra que o catálogo não tem e os parâmetros que a regra recusa deixam o
/// critério fora, com a recusa no ponteiro dele. A etapa citada segue o mesmo tratamento da eliminação.
/// </summary>
internal static class LeituraDosCriteriosDeDesempate
{
    /// <summary>O nome da lista na origem do documento.</summary>
    internal const string Lista = "classificacao.criteriosDeDesempate";

    internal static ItensLidosDaClassificacao<CriterioDesempate> Ler(
        IReadOnlyList<CriterioDeDesempateNoDocumento> criterios, EtapasLidas etapas, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(cadastros);

        return ItensLidosDaClassificacao<CriterioDesempate>.De(
            Lista, criterios, static criterio => criterio.PosicaoNoDocumento, (criterio, adotados) => Ler(criterio, adotados + 1, etapas, cadastros));
    }

    private static Result<CriterioDesempate>? Ler(
        CriterioDeDesempateNoDocumento criterio, int ordem, EtapasLidas etapas, CadastrosCitadosNoDocumento cadastros)
    {
        RegraDoCatalogoCitada? doCatalogo = cadastros.Regra(criterio.Regra.Codigo, criterio.Regra.Versao);
        if (RecusaDoCatalogo(doCatalogo) is { } foraDoCatalogo)
        {
            return Recusa(criterio, new FieldError("regraCodigo", foraDoCatalogo));
        }

        EtapaCitadaNoDocumento? etapa = criterio.Parametros.EtapaChave is { } chave ? EtapaCitadaNoDocumento.De(chave, etapas) : null;
        return etapa switch
        {
            { RecusadaNaLeitura: true } => null,
            { Inexistente: true } => Recusa(
                criterio, new FieldError("etapaRef", ReferenciaAEtapaInexistente.PeloDesempateNaPosicao(criterio.PosicaoNoDocumento + 1))),
            _ => Criar(criterio, ordem, doCatalogo!.Referencia, etapa?.Id, cadastros),
        };
    }

    private static Result<CriterioDesempate> Criar(
        CriterioDeDesempateNoDocumento criterio, int ordem, ReferenciaRegra referencia, Guid? etapaRef, CadastrosCitadosNoDocumento cadastros)
    {
        Result<ParametrosCriterioDesempate> parametros = Parametros(criterio, referencia.Codigo, etapaRef);
        if (parametros.IsFailure)
        {
            return Recusa(criterio, parametros.Errors);
        }

        // O vocabulário vai sempre: nulo pularia a conferência do dado. O que a coleta declara é conferido à parte, e os dados coletados seguem nulos.
        Result<CriterioDesempate> criado = CriterioDesempate.Criar(
            ordem, referencia, parametros.Value!, cadastros.DadosDoDesempate, fatosColetadosPeloProcesso: null);
        return criado.IsSuccess ? criado : Recusa(criterio, criado.Errors);
    }

    private static DomainError? RecusaDoCatalogo(RegraDoCatalogoCitada? regra) => regra switch
    {
        null => new DomainError("CriterioDesempate.RegraNaoEncontrada", "Regra de desempate não encontrada no catálogo de regras."),
        { Tipo: not TipoRegra.CriterioDesempate } => new DomainError("CriterioDesempate.RegraTipoInvalido", "A regra não é do tipo criterio_desempate."),
        _ => null,
    };

    private static Result<ParametrosCriterioDesempate> Parametros(CriterioDeDesempateNoDocumento criterio, string codigo, Guid? etapaRef) =>
        CatalogoDe<RegraDeDesempate>.TentarObter(codigo, out RegraDeDesempate tipo)
            ? DoTipo(criterio, tipo, etapaRef)
            : Falha("regraCodigo", "CriterioDesempate.RegraTipoInvalido", "O código da regra de desempate não é conhecido.");

    private static Result<ParametrosCriterioDesempate> DoTipo(CriterioDeDesempateNoDocumento criterio, RegraDeDesempate tipo, Guid? etapaRef) => tipo switch
    {
        RegraDeDesempate.MaiorNotaNaEtapa => etapaRef is { } etapa
            ? Sucesso(new ParametrosDesempateMaiorNotaEtapa(etapa))
            : Falha("etapaRef", "CriterioDesempate.EtapaRefObrigatorio", "O critério de desempate pela maior nota na etapa exige a etapa."),
        RegraDeDesempate.MaiorIdade => Sucesso(new ParametrosDesempateMaiorIdade()),
        RegraDeDesempate.Idoso => criterio.Parametros.IdadeMinima is { } idade
            ? Sucesso(new ParametrosDesempateIdoso(idade))
            : Falha("idadeMinima", "CriterioDesempate.IdadeMinimaObrigatoria", "O critério de desempate do idoso exige a idade mínima."),
        RegraDeDesempate.MaiorNotaPorAreaDoEnem => Sucesso(new ParametrosDesempateMaiorNotaAreaEnem(Areas(criterio.Parametros))),
        RegraDeDesempate.CondicaoSobreDado => ParametrosDesempatePredicadoFato.Criar(
            criterio.Parametros.CodigoDoDado, criterio.Parametros.Operador, criterio.Parametros.Valor),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
    };

    // Um item além do teto basta para a recusa da lista, e o resto nem é lido: a entrada pequena não gera resposta desproporcional.
    private static string[] Areas(ParametrosDoDesempateNoDocumento parametros) =>
        [.. parametros.Areas.Take(ParametrosDesempateMaiorNotaAreaEnem.AreasMaximo + 1)];

    private static Result<ParametrosCriterioDesempate> Sucesso(ParametrosCriterioDesempate parametros) =>
        Result<ParametrosCriterioDesempate>.Success(parametros);

    private static Result<ParametrosCriterioDesempate> Falha(string campo, string codigo, string mensagem) =>
        Result<ParametrosCriterioDesempate>.ValidationFailure([new(campo, new DomainError(codigo, mensagem))]);

    private static Result<CriterioDesempate> Recusa(CriterioDeDesempateNoDocumento criterio, params IReadOnlyList<FieldError> causas) =>
        Result<CriterioDesempate>.ValidationFailure(
            [.. causas.Select(causa => RegrasDoDesempate.CampoDoCriterioDesempate(criterio.PosicaoNoDocumento, causa))]);
}
