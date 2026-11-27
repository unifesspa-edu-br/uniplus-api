namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As regras de eliminação do documento. A regra que o catálogo não tem, ou que não é de eliminação, e a que traz parâmetros que o
/// código dela não aceita ficam fora da declaração com a recusa no ponteiro dela. A etapa citada é a do documento pela chave: a que a
/// leitura adotou vira o id em memória, a recusada deixa a regra fora sem causa nova, e a que o documento não tem é recusada sem
/// repetir o texto enviado.
/// </summary>
internal static class LeituraDasRegrasDeEliminacao
{
    /// <summary>O nome da lista na origem do documento.</summary>
    internal const string Lista = "classificacao.regrasEliminacao";

    private const string ListaNoBloco = "regrasEliminacao";

    internal static ItensLidosDaClassificacao<RegraEliminacao> Ler(
        IReadOnlyList<RegraDeEliminacaoNoDocumento> regras, EtapasLidas etapas, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(cadastros);

        return ItensLidosDaClassificacao<RegraEliminacao>.De(
            Lista, regras, static regra => regra.PosicaoNoDocumento, (regra, _) => Ler(regra, etapas, cadastros));
    }

    private static Result<RegraEliminacao>? Ler(RegraDeEliminacaoNoDocumento regra, EtapasLidas etapas, CadastrosCitadosNoDocumento cadastros)
    {
        RegraDoCatalogoCitada? doCatalogo = cadastros.Regra(regra.Regra.Codigo, regra.Regra.Versao);
        if (RecusaDoCatalogo(doCatalogo) is { } foraDoCatalogo)
        {
            return Recusa(regra, "regra.codigo", foraDoCatalogo);
        }

        EtapaCitadaNoDocumento? etapa = regra.EtapaChave is { } chave ? EtapaCitadaNoDocumento.De(chave, etapas) : null;
        return etapa switch
        {
            { RecusadaNaLeitura: true } => null,
            { Inexistente: true } => Recusa(regra, "etapaRef", ReferenciaAEtapaInexistente.PelaEliminacaoNaPosicao(regra.PosicaoNoDocumento + 1)),
            _ => Criar(regra, doCatalogo!.Referencia, etapa?.Id),
        };
    }

    private static Result<RegraEliminacao> Criar(RegraDeEliminacaoNoDocumento regra, ReferenciaRegra referencia, Guid? etapaRef)
    {
        Result<ParametrosRegraEliminacao> parametros = Parametros(regra, referencia.Codigo, etapaRef);
        if (parametros.IsFailure)
        {
            return Recusa(regra, null, parametros.Error!);
        }

        Result<RegraEliminacao> criada = RegraEliminacao.Criar(referencia, parametros.Value!);
        return criada.IsSuccess ? criada : Recusa(regra, null, criada.Error!);
    }

    private static DomainError? RecusaDoCatalogo(RegraDoCatalogoCitada? regra) => regra switch
    {
        null => new DomainError("RegraEliminacao.RegraNaoEncontrada", "Regra de eliminação não encontrada no catálogo de regras."),
        { Tipo: not TipoRegra.RegraEliminacao } => new DomainError("RegraEliminacao.RegraTipoInvalido", "A regra não é do tipo regra_eliminacao."),
        _ => null,
    };

    private static Result<ParametrosRegraEliminacao> Parametros(RegraDeEliminacaoNoDocumento regra, string codigo, Guid? etapaRef) =>
        CatalogoDe<RegraDeEliminacaoPorCalculo>.TentarObter(codigo, out RegraDeEliminacaoPorCalculo tipo)
            ? DoTipo(regra, tipo, etapaRef)
            : Falha("RegraEliminacao.RegraTipoInvalido", "O código da regra de eliminação não é conhecido.");

    private static Result<ParametrosRegraEliminacao> DoTipo(RegraDeEliminacaoNoDocumento regra, RegraDeEliminacaoPorCalculo tipo, Guid? etapaRef) =>
        tipo switch
        {
            RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa => NotaMinimaNaEtapa(regra, etapaRef),
            RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem => CorteEmArea(regra, etapaRef),
            RegraDeEliminacaoPorCalculo.AusenciaNaEtapa => AusenciaNaEtapa(regra, etapaRef),
            RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem => SemParametros(regra, etapaRef, new ParametrosEliminacaoPorZeroEmAreaDoEnem()),
            RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem => SemParametros(regra, etapaRef, new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem()),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
        };

    private static Result<ParametrosRegraEliminacao> NotaMinimaNaEtapa(RegraDeEliminacaoNoDocumento regra, Guid? etapaRef) =>
        etapaRef is { } etapa && regra.NotaMinima is { } notaMinima && regra.Minimo is null && regra.AreaCodigo is null
            ? Sucesso(new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapa, notaMinima))
            : Falha("RegraEliminacao.EtapaRefENotaMinimaObrigatorios", "A regra de eliminação por nota mínima exige a etapa e a nota mínima, e não aceita mínimo nem área.");

    private static Result<ParametrosRegraEliminacao> CorteEmArea(RegraDeEliminacaoNoDocumento regra, Guid? etapaRef) =>
        regra.AreaCodigo is { } area && regra.Minimo is { } minimo && etapaRef is null && regra.NotaMinima is null
            ? Sucesso(new ParametrosEliminacaoPorCorteEmAreaDoEnem(area, minimo))
            : Falha("RegraEliminacao.AreaEMinimoObrigatorios", "A regra de eliminação por corte em área exige a área e o mínimo, e não aceita etapa nem nota mínima.");

    private static Result<ParametrosRegraEliminacao> AusenciaNaEtapa(RegraDeEliminacaoNoDocumento regra, Guid? etapaRef) =>
        etapaRef is { } etapa && regra.NotaMinima is null && regra.Minimo is null && regra.AreaCodigo is null
            ? Sucesso(new ParametrosEliminacaoPorAusenciaNaEtapa(etapa))
            : Falha("RegraEliminacao.EtapaRefObrigatorio", "A regra de eliminação por ausência exige a etapa, e não aceita nota mínima, mínimo nem área.");

    private static Result<ParametrosRegraEliminacao> SemParametros(RegraDeEliminacaoNoDocumento regra, Guid? etapaRef, ParametrosRegraEliminacao parametros) =>
        etapaRef is null && regra.NotaMinima is null && regra.Minimo is null && regra.AreaCodigo is null
            ? Sucesso(parametros)
            : Falha("RegraEliminacao.ParametrosIncompativeisComRegra", "A regra de eliminação não aceita parâmetros.");

    private static Result<ParametrosRegraEliminacao> Sucesso(ParametrosRegraEliminacao parametros) =>
        Result<ParametrosRegraEliminacao>.Success(parametros);

    private static Result<ParametrosRegraEliminacao> Falha(string codigo, string mensagem) =>
        Result<ParametrosRegraEliminacao>.Failure(new DomainError(codigo, mensagem));

    private static Result<RegraEliminacao> Recusa(RegraDeEliminacaoNoDocumento regra, string? campoNaRegra, DomainError erro) =>
        Result<RegraEliminacao>.ValidationFailure(
            [new FieldError(campoNaRegra, erro).NoCaminhoDoItem($"{ListaNoBloco}[{regra.PosicaoNoDocumento}]")]);
}
