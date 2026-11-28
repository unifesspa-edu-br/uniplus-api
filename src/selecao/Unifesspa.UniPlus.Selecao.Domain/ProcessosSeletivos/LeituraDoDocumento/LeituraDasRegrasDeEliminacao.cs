namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
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

        ParteCitadaNoDocumento? etapa = regra.EtapaChave is { } chave ? ParteCitadaNoDocumento.De(chave, etapas) : null;
        return etapa switch
        {
            { RecusadaNaLeitura: true } => null,
            { Inexistente: true } => Recusa(regra, "etapaRef", ReferenciaAEtapaInexistente.PelaEliminacaoNaPosicao(regra.PosicaoNoDocumento + 1)),
            _ => Criar(regra, doCatalogo!.Referencia, etapa?.Id),
        };
    }

    private static Result<RegraEliminacao> Criar(RegraDeEliminacaoNoDocumento regra, ReferenciaRegra referencia, Guid? etapaRef)
    {
        Result<ParametrosRegraEliminacao> parametros = ParametrosDeclaradosDaEliminacao.Montar(
            referencia.Codigo, etapaRef, regra.NotaMinima, regra.Minimo, regra.AreaCodigo);
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

    private static Result<RegraEliminacao> Recusa(RegraDeEliminacaoNoDocumento regra, string? campoNaRegra, DomainError erro) =>
        Result<RegraEliminacao>.ValidationFailure(
            [new FieldError(campoNaRegra, erro).NoCaminhoDoItem($"{ListaNoBloco}[{regra.PosicaoNoDocumento}]")]);
}
