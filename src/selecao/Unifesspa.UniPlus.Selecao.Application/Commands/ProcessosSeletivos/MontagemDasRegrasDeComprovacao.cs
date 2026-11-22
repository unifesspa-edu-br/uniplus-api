namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Compõe as regras informadas e propaga o grupo de repetição às regras contidas.</summary>
internal sealed class MontagemDasRegrasDeComprovacao(MontagemDoDocumentoExigido documentos)
{
    public async Task<Result<List<RegraDeComprovacao>>> MontarRegrasSemGrupoAsync(
        IReadOnlyList<RegraInformadaConferida> entradas, int ordemInicial, CancellationToken cancellationToken)
    {
        RegrasMontadas montados = await MontarRegrasAsync(entradas, ordemInicial, null, false, cancellationToken).ConfigureAwait(false);
        return montados.Recusas.Count > 0
            ? Result<List<RegraDeComprovacao>>.ValidationFailure(montados.Recusas) : Result<List<RegraDeComprovacao>>.Success(montados.Regras);
    }

    private async Task<RegrasMontadas> MontarRegrasAsync(
        IReadOnlyList<RegraInformadaConferida> entradas, int ordemInicial, string? grupoAncestral, bool repeticaoRecusada, CancellationToken cancellationToken)
    {
        RegrasMontadas montados = new();
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            RegraMontada regra = await MontarRegraAsync(entradas[indice], ordemInicial + indice, grupoAncestral, repeticaoRecusada, cancellationToken)
                .ConfigureAwait(false);
            montados.Adicionar(regra);
        }
        return montados;
    }

    private async Task<RegraMontada> MontarRegraAsync(
        RegraInformadaConferida regra, int ordem, string? grupoAncestral, bool repeticaoRecusada, CancellationToken cancellationToken)
    {
        bool recusada = repeticaoRecusada || RegraInformadaConferida.RepeticaoRecusada(regra);
        string? grupo = grupoAncestral ?? (recusada ? null : regra.Entrada.GrupoDoFormularioParaRepeticao);
        RegrasMontadas itens = await MontarRegrasAsync(regra.Itens, 0, grupo, recusada, cancellationToken).ConfigureAwait(false);
        List<FieldError> erros = [.. regra.Recusas, .. itens.Recusas];
        erros.AddRange(RecusasDaComposicao(regra, itens));
        if (erros.Count > 0 || !itens.TodosMontados)
        {
            return new(null, erros);
        }
        return await MontarRegraConferidaAsync(regra, ordem, grupo, recusada, itens.Regras, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<FieldError> RecusasDaComposicao(RegraInformadaConferida regra, RegrasMontadas itens)
    {
        if (!itens.TodosMontados || itens.Regras.Count == 0 || regra.Declaracao.Forma is not (FormaDeComprovacao.TodosOsItens or FormaDeComprovacao.QuantidadeMinimaDeItens))
        {
            return [];
        }
        string? repeticao = RegraInformadaConferida.RepeticaoRecusada(regra) ? null : regra.Entrada.GrupoDoFormularioParaRepeticao;
        return RegraDeComprovacao.ConferirComposicao(itens.Regras, repeticao).Select(erro => NoItem(erro, regra.Caminho, regra.Indice));
    }

    private async Task<RegraMontada> MontarRegraConferidaAsync(
        RegraInformadaConferida regra, int ordem, string? grupo, bool repeticaoRecusada, List<RegraDeComprovacao> itens, CancellationToken cancellationToken)
    {
        if (regra.Declaracao.Forma == FormaDeComprovacao.Documento && repeticaoRecusada && regra.Entrada.Documento?.Condicoes().Count > 0)
        {
            return new(null, []);
        }
        Result<RegraDeComprovacao> resultado = regra.Declaracao.Forma == FormaDeComprovacao.Documento
            ? await MontarDocumentoAsync(regra.Entrada, ordem, grupo, cancellationToken).ConfigureAwait(false)
            : CriarGrupo(regra.Entrada, ordem, regra.Declaracao, itens);
        return resultado.IsFailure ? new(null, resultado.Errors) : new(resultado.Value, []);
    }

    private static FieldError NoItem(FieldError erro, string caminho, int indice) =>
        erro.NoCaminhoDe(caminho) with { Error = erro.Error with { Message = $"No item {indice + 1} da lista: {erro.Error.Message}" } };

    private sealed record RegraMontada(RegraDeComprovacao? Regra, IReadOnlyList<FieldError> Recusas);

    private sealed class RegrasMontadas
    {
        internal List<RegraDeComprovacao> Regras { get; } = [];
        internal List<FieldError> Recusas { get; } = [];
        internal bool TodosMontados { get; private set; } = true;

        internal void Adicionar(RegraMontada regra)
        {
            Recusas.AddRange(regra.Recusas);
            if (regra.Regra is { } montado)
            {
                Regras.Add(montado);
            }
            else
            {
                TodosMontados = false;
            }
        }
    }

    private async Task<Result<RegraDeComprovacao>> MontarDocumentoAsync(
        RegraDeComprovacaoInformada regra, int ordem, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        if (regra.Documento is not { } regraDeDocumento)
        {
            return Result<RegraDeComprovacao>.ValidationFailure(DeclaracaoDaRegraDeComprovacao.Interpretar(regra.Forma, temDocumento: false, regra.Itens?.Count ?? 0, regra.QuantidadeMinima(), regra.ConsequenciaDoGrupo(), regra.BasesLegaisDoGrupo().Count > 0, DistincaoDe(regra)).Recusas);
        }

        Result<DocumentoExigido> documento = await documentos.MontarAsync(regraDeDocumento, grupoDaRepeticao, cancellationToken).ConfigureAwait(false);
        return documento.IsFailure
            ? Result<RegraDeComprovacao>.ValidationFailure(documento.Errors)
            : CriarRegraDeDocumento(regra, documento.Value!, ordem);
    }

    private static Result<RegraDeComprovacao> CriarRegraDeDocumento(RegraDeComprovacaoInformada regra, DocumentoExigido documento, int ordem)
    {
        DeclaracaoDaRegraDeComprovacao declaracao = Declarar(regra);
        return RegraDeComprovacao.CriarRegraDeDocumento(documento, ordem, declaracao.QuantidadeMinima,
            regra.Distincao()?.CriterioDeDistincao is { } chave ? CriterioDeDistincaoDasApresentacoesCodigo.FromCodigo(chave) : null,
            regra.Distincao()?.DataReferencia, regra.Distincao()?.OcorrenciasEsperadas, regra.GrupoDoFormularioParaRepeticao);
    }

    private static Result<RegraDeComprovacao> CriarGrupo(RegraDeComprovacaoInformada regra, int ordem, DeclaracaoDaRegraDeComprovacao declaracao, IReadOnlyList<RegraDeComprovacao> itens)
    {
        Result<IReadOnlyList<BaseLegalDaComprovacao>> basesLegais = BasesLegaisDoGrupo(regra.BasesLegaisDoGrupo());
        return basesLegais.IsFailure
            ? Result<RegraDeComprovacao>.ValidationFailure(basesLegais.Errors)
            : RegraDeComprovacao.CriarGrupo(
                declaracao.Forma!.Value, ordem, declaracao.QuantidadeMinima, declaracao.Consequencia, basesLegais.Value!, itens, regra.GrupoDoFormularioParaRepeticao);
    }

    private static DeclaracaoDaRegraDeComprovacao Declarar(RegraDeComprovacaoInformada regra) =>
        DeclaracaoDaRegraDeComprovacao.Interpretar(regra.Forma, regra.Documento is not null, regra.Itens?.Count ?? 0,
            regra.QuantidadeMinima(), regra.ConsequenciaDoGrupo(), regra.BasesLegaisDoGrupo().Count > 0, DistincaoDe(regra));

    private static Result<IReadOnlyList<BaseLegalDaComprovacao>> BasesLegaisDoGrupo(IReadOnlyList<BaseLegalInput> declaradas) =>
        EscritaDasBasesLegais.Resolver(
            declaradas,
            static b => BaseLegalDaComprovacao.ValidarFormaBasica(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b)),
            static b => BaseLegalDaComprovacao.Criar(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b), b.Observacao).Value!);
    private static DistincaoDeclarada DistincaoDe(RegraDeComprovacaoInformada regra) =>
        DistincaoDeclarada.Interpretar(regra.Distincao()?.CriterioDeDistincao, regra.Distincao()?.DataReferencia, regra.Distincao()?.OcorrenciasEsperadas);

}
