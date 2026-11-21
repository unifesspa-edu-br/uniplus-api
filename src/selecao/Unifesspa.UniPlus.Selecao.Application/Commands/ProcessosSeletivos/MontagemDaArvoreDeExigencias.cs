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
        IReadOnlyList<NoExigenciaInput> entradas, int ordemInicial, CancellationToken cancellationToken)
    {
        List<NoExigencia> raizes = [];
        int ordem = ordemInicial;
        foreach (NoExigenciaInput entrada in entradas)
        {
            Result<NoExigencia> raiz = await NoAsync(entrada, ordem++, grupoAncestral: null, cancellationToken).ConfigureAwait(false);
            if (raiz.IsFailure)
            {
                return Result<List<NoExigencia>>.ValidationFailure(raiz.Errors);
            }

            raizes.Add(raiz.Value!);
        }

        return Result<List<NoExigencia>>.Success(raizes);
    }

    private Task<Result<NoExigencia>> NoAsync(
        NoExigenciaInput no, int ordem, string? grupoAncestral, CancellationToken cancellationToken)
    {
        string? grupoDaRepeticao = grupoAncestral ?? no.RepetePorEntidade;
        return ArvoreDeEntradaDasExigencias.EhFolha(no)
            ? FolhaAsync(no, ordem, grupoDaRepeticao, cancellationToken)
            : GrupoAsync(no, ordem, grupoDaRepeticao, cancellationToken);
    }

    private async Task<Result<NoExigencia>> FolhaAsync(
        NoExigenciaInput no, int ordem, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        if (no.Documento is not { } folha)
        {
            return Result<NoExigencia>.Failure(DocumentoAusenteNaFolha());
        }

        Result<DocumentoExigido> documento = await documentos.MontarAsync(folha, grupoDaRepeticao, cancellationToken).ConfigureAwait(false);
        return documento.IsFailure
            ? Result<NoExigencia>.ValidationFailure(documento.Errors)
            : CriarFolha(no, documento.Value!, ordem);
    }

    private static Result<NoExigencia> CriarFolha(NoExigenciaInput no, DocumentoExigido documento, int ordem) =>
        NoExigencia.CriarFolha(
            documento,
            ordem,
            no.QuantidadeMinima(),
            no.Distincao()?.Chave is { } chave ? ChaveDistincaoCodigo.FromCodigo(chave) : null,
            no.Distincao()?.DataReferencia,
            no.Distincao()?.OcorrenciasEsperadas,
            no.RepetePorEntidade);

    private async Task<Result<NoExigencia>> GrupoAsync(
        NoExigenciaInput no, int ordem, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        TipoNo tipo = TipoDoGrupo(no.Tipo);
        if (tipo == TipoNo.Nenhum)
        {
            return Result<NoExigencia>.Failure(TipoDeNoInvalido(no.Tipo));
        }

        Result<List<NoExigencia>> filhos = await FilhosAsync(no, grupoDaRepeticao, cancellationToken).ConfigureAwait(false);
        return filhos.IsFailure
            ? Result<NoExigencia>.ValidationFailure(filhos.Errors)
            : CriarGrupo(no, tipo, ordem, filhos.Value!);
    }

    private static Result<NoExigencia> CriarGrupo(NoExigenciaInput no, TipoNo tipo, int ordem, List<NoExigencia> filhos)
    {
        Result<IReadOnlyList<NoExigenciaBaseLegal>> basesLegais = BasesLegaisDoGrupo(no.BasesLegaisDoGrupo());
        return basesLegais.IsFailure
            ? Result<NoExigencia>.ValidationFailure(basesLegais.Errors)
            : NoExigencia.CriarGrupo(
                tipo, ordem, no.QuantidadeMinima(), no.ConsequenciaDoGrupo(), basesLegais.Value!, filhos, no.RepetePorEntidade);
    }

    private static TipoNo TipoDoGrupo(string tipo) => tipo switch
    {
        "E" => TipoNo.GrupoE,
        "OU" => TipoNo.GrupoOu,
        _ => TipoNo.Nenhum,
    };

    private static DomainError DocumentoAusenteNaFolha() =>
        new("NoExigencia.DocumentoObrigatorioEmFolha", "Um nó do tipo FOLHA precisa declarar 'documento'.");

    private static DomainError TipoDeNoInvalido(string tipo) =>
        new("NoExigencia.TipoInvalido", $"Tipo de nó '{tipo}' inválido — esperado FOLHA, E ou OU.");

    private async Task<Result<List<NoExigencia>>> FilhosAsync(
        NoExigenciaInput grupo, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        List<NoExigencia> filhos = [];
        int ordem = 0;
        foreach (NoExigenciaInput filhoInput in grupo.Filhos ?? [])
        {
            Result<NoExigencia> filho = await NoAsync(filhoInput, ordem++, grupoDaRepeticao, cancellationToken).ConfigureAwait(false);
            if (filho.IsFailure)
            {
                return Result<List<NoExigencia>>.ValidationFailure(filho.Errors);
            }

            filhos.Add(filho.Value!);
        }

        return Result<List<NoExigencia>>.Success(filhos);
    }

    private static Result<IReadOnlyList<NoExigenciaBaseLegal>> BasesLegaisDoGrupo(IReadOnlyList<BaseLegalInput> declaradas) =>
        EscritaDasBasesLegais.Resolver(
            declaradas,
            static b => NoExigenciaBaseLegal.ValidarFormaBasica(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Status(b)),
            static b => NoExigenciaBaseLegal.Criar(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Status(b), b.Observacao).Value!);
}
