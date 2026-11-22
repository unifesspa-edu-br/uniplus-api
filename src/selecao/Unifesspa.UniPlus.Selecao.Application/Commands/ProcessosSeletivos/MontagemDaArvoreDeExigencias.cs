namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Monta a floresta de exigências a partir do payload. A construção é de baixo para cima — o grupo recebe
/// os filhos já prontos —, mas a visita é de cima para baixo, o que permite propagar o grupo da repetição
/// descendo. A árvore falha no primeiro nó com defeito; entre nós, acumular exigiria um caminho posicional
/// sem convenção estabelecida.
/// </summary>
internal sealed class MontagemDaArvoreDeExigencias(MontagemDoDocumentoExigido documentos)
{
    public async Task<Result<List<NoExigencia>>> MontarRaizesAsync(
        IReadOnlyList<NoDeEntradaConferido> entradas, int ordemInicial, CancellationToken cancellationToken)
    {
        NosMontados montados = await MontarNosAsync(entradas, ordemInicial, null, false, cancellationToken).ConfigureAwait(false);
        return montados.Recusas.Count > 0
            ? Result<List<NoExigencia>>.ValidationFailure(montados.Recusas) : Result<List<NoExigencia>>.Success(montados.Nos);
    }

    private async Task<NosMontados> MontarNosAsync(
        IReadOnlyList<NoDeEntradaConferido> entradas, int ordemInicial, string? grupoAncestral, bool repeticaoRecusada, CancellationToken cancellationToken)
    {
        NosMontados montados = new();
        for (int indice = 0; indice < entradas.Count; indice++)
        {
            NoMontado no = await NoAsync(entradas[indice], ordemInicial + indice, grupoAncestral, repeticaoRecusada, cancellationToken)
                .ConfigureAwait(false);
            montados.Adicionar(no);
        }
        return montados;
    }

    private async Task<NoMontado> NoAsync(
        NoDeEntradaConferido no, int ordem, string? grupoAncestral, bool repeticaoRecusada, CancellationToken cancellationToken)
    {
        bool recusada = repeticaoRecusada || NoDeEntradaConferido.RepeticaoRecusada(no);
        string? grupo = grupoAncestral ?? (recusada ? null : no.Entrada.RepetePorEntidade);
        NosMontados filhos = await MontarNosAsync(no.Filhos, 0, grupo, recusada, cancellationToken).ConfigureAwait(false);
        List<FieldError> erros = [.. no.Recusas, .. filhos.Recusas];
        erros.AddRange(RecusasDaComposicao(no, filhos));
        if (erros.Count > 0 || !filhos.TodosMontados)
        {
            return new(null, erros);
        }
        return await MontarNoConferidoAsync(no, ordem, grupo, recusada, filhos.Nos, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<FieldError> RecusasDaComposicao(NoDeEntradaConferido no, NosMontados filhos)
    {
        if (!filhos.TodosMontados || filhos.Nos.Count == 0 || no.Declaracao.Tipo is not (TipoNo.GrupoE or TipoNo.GrupoOu))
        {
            return [];
        }
        string? repeticao = NoDeEntradaConferido.RepeticaoRecusada(no) ? null : no.Entrada.RepetePorEntidade;
        return NoExigencia.ConferirComposicao(filhos.Nos, repeticao).Select(erro => NoItem(erro, no.Caminho, no.Indice));
    }

    private async Task<NoMontado> MontarNoConferidoAsync(
        NoDeEntradaConferido no, int ordem, string? grupo, bool repeticaoRecusada, List<NoExigencia> filhos, CancellationToken cancellationToken)
    {
        if (no.Declaracao.Tipo == TipoNo.Folha && repeticaoRecusada && no.Entrada.Documento?.Condicoes().Count > 0)
        {
            return new(null, []);
        }
        Result<NoExigencia> resultado = no.Declaracao.Tipo == TipoNo.Folha
            ? await FolhaAsync(no.Entrada, ordem, grupo, cancellationToken).ConfigureAwait(false)
            : CriarGrupo(no.Entrada, ordem, no.Declaracao, filhos);
        return resultado.IsFailure ? new(null, resultado.Errors) : new(resultado.Value, []);
    }

    private static FieldError NoItem(FieldError erro, string caminho, int indice) =>
        erro.NoCaminhoDe(caminho) with { Error = erro.Error with { Message = $"No item {indice + 1} da lista: {erro.Error.Message}" } };

    private sealed record NoMontado(NoExigencia? No, IReadOnlyList<FieldError> Recusas);

    private sealed class NosMontados
    {
        internal List<NoExigencia> Nos { get; } = [];
        internal List<FieldError> Recusas { get; } = [];
        internal bool TodosMontados { get; private set; } = true;

        internal void Adicionar(NoMontado no)
        {
            Recusas.AddRange(no.Recusas);
            if (no.No is { } montado)
            {
                Nos.Add(montado);
            }
            else
            {
                TodosMontados = false;
            }
        }
    }

    private async Task<Result<NoExigencia>> FolhaAsync(
        RegraDeComprovacaoInformada no, int ordem, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        if (no.Documento is not { } folha)
        {
            return Result<NoExigencia>.ValidationFailure(DeclaracaoDoNoDeExigencia.Interpretar(no.Tipo, temDocumento: false, no.Filhos?.Count ?? 0, no.QuantidadeMinima(), no.ConsequenciaDoGrupo(), no.BasesLegaisDoGrupo().Count > 0, DistincaoDe(no)).Recusas);
        }

        Result<DocumentoExigido> documento = await documentos.MontarAsync(folha, grupoDaRepeticao, cancellationToken).ConfigureAwait(false);
        return documento.IsFailure
            ? Result<NoExigencia>.ValidationFailure(documento.Errors)
            : CriarFolha(no, documento.Value!, ordem);
    }

    private static Result<NoExigencia> CriarFolha(RegraDeComprovacaoInformada no, DocumentoExigido documento, int ordem)
    {
        DeclaracaoDoNoDeExigencia declaracao = Declarar(no);
        return NoExigencia.CriarFolha(documento, ordem, declaracao.QuantidadeMinima,
            no.Distincao()?.Chave is { } chave ? ChaveDistincaoCodigo.FromCodigo(chave) : null,
            no.Distincao()?.DataReferencia, no.Distincao()?.OcorrenciasEsperadas, no.RepetePorEntidade);
    }

    private static Result<NoExigencia> CriarGrupo(RegraDeComprovacaoInformada no, int ordem, DeclaracaoDoNoDeExigencia declaracao, IReadOnlyList<NoExigencia> filhos)
    {
        Result<IReadOnlyList<BaseLegalDaComprovacao>> basesLegais = BasesLegaisDoGrupo(no.BasesLegaisDoGrupo());
        return basesLegais.IsFailure
            ? Result<NoExigencia>.ValidationFailure(basesLegais.Errors)
            : NoExigencia.CriarGrupo(
                declaracao.Tipo, ordem, declaracao.QuantidadeMinima, declaracao.Consequencia, basesLegais.Value!, filhos, no.RepetePorEntidade);
    }

    private static DeclaracaoDoNoDeExigencia Declarar(RegraDeComprovacaoInformada no) =>
        DeclaracaoDoNoDeExigencia.Interpretar(no.Tipo, no.Documento is not null, no.Filhos?.Count ?? 0,
            no.QuantidadeMinima(), no.ConsequenciaDoGrupo(), no.BasesLegaisDoGrupo().Count > 0, DistincaoDe(no));

    private static Result<IReadOnlyList<BaseLegalDaComprovacao>> BasesLegaisDoGrupo(IReadOnlyList<BaseLegalInput> declaradas) =>
        EscritaDasBasesLegais.Resolver(
            declaradas,
            static b => BaseLegalDaComprovacao.ValidarFormaBasica(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Status(b)),
            static b => BaseLegalDaComprovacao.Criar(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Status(b), b.Observacao).Value!);
    private static DistincaoDeclarada DistincaoDe(RegraDeComprovacaoInformada no) =>
        DistincaoDeclarada.Interpretar(no.Distincao()?.Chave, no.Distincao()?.DataReferencia, no.Distincao()?.OcorrenciasEsperadas);

}
