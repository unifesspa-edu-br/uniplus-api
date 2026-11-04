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
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.DocumentoObrigatorioEmFolha",
                "Um nó do tipo FOLHA precisa declarar 'documento'."));
        }

        Result<DocumentoExigido> documento = await documentos.MontarAsync(folha, grupoDaRepeticao, cancellationToken).ConfigureAwait(false);
        return documento.IsFailure
            ? Result<NoExigencia>.ValidationFailure(documento.Errors)
            : NoExigencia.CriarFolha(
                documento.Value!,
                ordem,
                no.QuantidadeMinima,
                no.ChaveDistincao is null ? null : ChaveDistincaoCodigo.FromCodigo(no.ChaveDistincao),
                no.DataReferencia,
                no.OcorrenciasEsperadas,
                no.RepetePorEntidade);
    }

    private async Task<Result<NoExigencia>> GrupoAsync(
        NoExigenciaInput no, int ordem, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        TipoNo tipo = no.Tipo switch
        {
            "E" => TipoNo.GrupoE,
            "OU" => TipoNo.GrupoOu,
            _ => TipoNo.Nenhum,
        };
        if (tipo == TipoNo.Nenhum)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.TipoInvalido",
                $"Tipo de nó '{no.Tipo}' inválido — esperado FOLHA, E ou OU."));
        }

        Result<List<NoExigencia>> filhos = await FilhosAsync(no, grupoDaRepeticao, cancellationToken).ConfigureAwait(false);
        if (filhos.IsFailure)
        {
            return Result<NoExigencia>.ValidationFailure(filhos.Errors);
        }

        Result<IReadOnlyList<NoExigenciaBaseLegal>> basesLegais = BasesLegaisDoGrupo(no.BasesLegais ?? []);
        return basesLegais.IsFailure
            ? Result<NoExigencia>.ValidationFailure(basesLegais.Errors)
            : NoExigencia.CriarGrupo(
                tipo, ordem, no.QuantidadeMinima, no.Consequencia, basesLegais.Value!, filhos.Value!, no.RepetePorEntidade);
    }

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
        BasesLegaisDoPayload.Resolver(
            declaradas,
            static b => NoExigenciaBaseLegal.ValidarFormaBasica(b.Referencia, BasesLegaisDoPayload.Abrangencia(b), BasesLegaisDoPayload.Status(b)),
            static b => NoExigenciaBaseLegal.Criar(b.Referencia, BasesLegaisDoPayload.Abrangencia(b), BasesLegaisDoPayload.Status(b), b.Observacao).Value!);
}
