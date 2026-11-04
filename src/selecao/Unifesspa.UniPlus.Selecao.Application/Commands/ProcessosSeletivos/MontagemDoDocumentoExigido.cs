namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta o documento de UMA folha da árvore: tipo de documento, aplicabilidade, gatilho, bases legais,
/// idade, formato, tamanho e modelo. A posição na árvore substitui o antigo agrupamento de satisfação.
/// </summary>
internal sealed class MontagemDoDocumentoExigido(ContextoDaMontagemDasExigencias contexto)
{
    public async Task<Result<DocumentoExigido>> MontarAsync(
        ItemDocumentoExigidoInput folha, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        Result<TipoDocumentoView> tipo = await TipoDeDocumentoAsync(folha, cancellationToken).ConfigureAwait(false);
        if (tipo.IsFailure)
        {
            return Result<DocumentoExigido>.ValidationFailure(tipo.Errors);
        }

        Result<IReadOnlyList<CondicaoGatilho>> condicoes = Condicoes(folha, grupoDaRepeticao);
        if (condicoes.IsFailure)
        {
            return Result<DocumentoExigido>.ValidationFailure(condicoes.Errors);
        }

        Result<IReadOnlyList<DocumentoExigidoBaseLegal>> basesLegais = BasesLegais(folha);
        return basesLegais.IsFailure
            ? Result<DocumentoExigido>.ValidationFailure(basesLegais.Errors)
            : Criar(folha, tipo.Value!, condicoes.Value!, basesLegais.Value!);
    }

    private async Task<Result<TipoDocumentoView>> TipoDeDocumentoAsync(ItemDocumentoExigidoInput folha, CancellationToken cancellationToken)
    {
        TipoDocumentoView? tipo = await contexto.TiposDeDocumento
            .ObterPorIdAsync(folha.TipoDocumentoId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result<TipoDocumentoView>.Failure(new DomainError(
                "DocumentoExigido.TipoDocumentoNaoEncontrado",
                $"Tipo de documento {folha.TipoDocumentoId} não encontrado ou não está mais vivo."));
        }

        // O tipo sem a proteção aprovada só continua onde o processo já o exige: o vínculo novo é recusado, e
        // a publicação barra o que ficou pendente depois de vinculado (ADR-0153).
        return !tipo.ProtecaoAprovada && contexto.Processo.DocumentosExigidos.All(d => d.TipoDocumentoOrigemId != tipo.Id)
            ? Result<TipoDocumentoView>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.TipoDocumentoNoVinculoComProtecaoPendente,
                $"O tipo de documento '{tipo.Codigo}' aguarda a aprovação da proteção de dados e não aceita vínculo novo."))
            : Result<TipoDocumentoView>.Success(tipo);
    }

    private Result<IReadOnlyList<CondicaoGatilho>> Condicoes(ItemDocumentoExigidoInput folha, string? grupoDaRepeticao) =>
        contexto.Fatos is { } fatos
            ? ConferenciaDasCondicoesDaExigencia.Resolver(folha, contexto.Processo, fatos, grupoDaRepeticao)
            : Result<IReadOnlyList<CondicaoGatilho>>.Success([]);

    private static Result<IReadOnlyList<DocumentoExigidoBaseLegal>> BasesLegais(ItemDocumentoExigidoInput folha) =>
        EscritaDasBasesLegais.Resolver(
            folha.BasesLegais,
            static b => DocumentoExigidoBaseLegal.ValidarFormaBasica(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Status(b)),
            static b => DocumentoExigidoBaseLegal.Criar(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Status(b), b.Observacao).Value!);

    private Result<DocumentoExigido> Criar(
        ItemDocumentoExigidoInput folha,
        TipoDocumentoView tipo,
        IReadOnlyList<CondicaoGatilho> condicoes,
        IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais)
    {
        Result<EscritaDasRestricoesDoDocumento> restricoes = EscritaDasRestricoesDoDocumento.Resolver(folha, contexto.Modelos);
        return restricoes.IsFailure
            ? Result<DocumentoExigido>.ValidationFailure(restricoes.Errors)
            : DocumentoExigido.Criar(
                folha.ExigidoNaFaseId,
                tipo.Id,
                tipo.Codigo,
                tipo.Nome,
                tipo.Categoria,
                new ProtecaoDoDocumento(tipo.ClassificacaoProtecao, tipo.FinalidadeTratamento, tipo.HipoteseLegal),
                EscritaDaExigencia.AplicabilidadeDoToken(folha.Aplicabilidade),
                folha.Obrigatorio,
                folha.ConsequenciaIndeferimento,
                condicoes,
                basesLegais,
                restricoes.Value!.Idade,
                restricoes.Value.Formatos,
                folha.TamanhoMaximoBytes,
                folha.ExigidoNaEtapaId,
                restricoes.Value.Modelo,
                EscritaDaExigencia.FinalidadeDoToken(folha.Finalidade),
                folha.FatosComprovados);
    }
}
