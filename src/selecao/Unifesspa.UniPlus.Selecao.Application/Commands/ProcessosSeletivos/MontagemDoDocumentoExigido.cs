namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Monta a exigência documental a partir dos dados informados.</summary>
internal sealed class MontagemDoDocumentoExigido(ContextoDaMontagemDasExigencias contexto)
{
    internal sealed record Partes(TipoDocumentoView Tipo, IReadOnlyList<CondicaoGatilho> Condicoes, EscritaDasRestricoesDoDocumento Restricoes);

    public async Task<Result<Partes>> ConferirReferenciasAsync(
        ItemDocumentoExigidoInput documento, RepeticaoDaRegra repeticao, CancellationToken cancellationToken)
    {
        Result<TipoDocumentoView> tipo = await TipoDeDocumentoAsync(documento, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyList<CondicaoGatilho>> condicoes = repeticao.Recusada
            ? Result<IReadOnlyList<CondicaoGatilho>>.Success([]) : Condicoes(documento, repeticao.Grupo);
        Result<EscritaDasRestricoesDoDocumento> restricoes = EscritaDasRestricoesDoDocumento.Resolver(documento, contexto.Modelos);
        return Reunir(tipo, condicoes, restricoes);
    }

    private static Result<Partes> Reunir(Result<TipoDocumentoView> tipo, Result<IReadOnlyList<CondicaoGatilho>> condicoes,
        Result<EscritaDasRestricoesDoDocumento> restricoes)
    {
        List<FieldError> erros = [.. tipo.Errors, .. condicoes.Errors, .. restricoes.Errors];
        return erros.Count > 0 ? Result<Partes>.ValidationFailure(erros)
            : Result<Partes>.Success(new(tipo.Value!, condicoes.Value!, restricoes.Value!));
    }

    public static Result<DocumentoExigido> Criar(ItemDocumentoExigidoInput documento, Partes partes)
    {
        Result<IReadOnlyList<DocumentoExigidoBaseLegal>> bases = BasesLegais(documento);
        return bases.IsFailure ? Result<DocumentoExigido>.ValidationFailure(bases.Errors)
            : CriarDocumento(documento, partes, bases.Value!);
    }

    private async Task<Result<TipoDocumentoView>> TipoDeDocumentoAsync(ItemDocumentoExigidoInput documentoInformado, CancellationToken cancellationToken)
    {
        TipoDocumentoView? tipo = await contexto.TiposDeDocumento
            .ObterPorIdAsync(documentoInformado.TipoDocumentoId(), cancellationToken).ConfigureAwait(false);
        return tipo is null
            ? Result<TipoDocumentoView>.Failure(new DomainError(
                "DocumentoExigido.TipoDocumentoNaoEncontrado",
                "O tipo de documento não foi encontrado ou não está mais disponível."))
            : VinculoPermitido(tipo);
    }

    // O tipo sem a proteção aprovada só continua onde o processo já o exige: o vínculo novo é recusado, e
    // a publicação barra o que ficou pendente depois de vinculado (ADR-0153).
    private Result<TipoDocumentoView> VinculoPermitido(TipoDocumentoView tipo) =>
        !tipo.ProtecaoAprovada && contexto.Processo.DocumentosExigidos.All(d => d.TipoDocumentoOrigemId != tipo.Id)
            ? Result<TipoDocumentoView>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.TipoDocumentoNoVinculoComProtecaoPendente,
                $"O tipo de documento '{tipo.Codigo}' aguarda a aprovação da proteção de dados e não aceita vínculo novo."))
            : Result<TipoDocumentoView>.Success(tipo);

    private Result<IReadOnlyList<CondicaoGatilho>> Condicoes(ItemDocumentoExigidoInput documentoInformado, string? grupoDaRepeticao) =>
        contexto.Fatos is { } fatos
            ? ConferenciaDasCondicoesDaExigencia.Resolver(documentoInformado, contexto.Processo, fatos, grupoDaRepeticao)
            : Result<IReadOnlyList<CondicaoGatilho>>.Success([]);

    private static Result<IReadOnlyList<DocumentoExigidoBaseLegal>> BasesLegais(ItemDocumentoExigidoInput documentoInformado) =>
        EscritaDasBasesLegais.Resolver(
            documentoInformado.BasesLegais(),
            static b => DocumentoExigidoBaseLegal.ValidarFormaBasica(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b)),
            static b => DocumentoExigidoBaseLegal.Criar(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b), b.Observacao).Value!);

    private static Result<DocumentoExigido> CriarDocumento(
        ItemDocumentoExigidoInput documentoInformado, Partes partes, IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais)
    {
        TipoDocumentoView tipo = partes.Tipo;
        EscritaDasRestricoesDoDocumento restricoes = partes.Restricoes;
        return DocumentoExigido.Criar(
            documentoInformado.FaseId(), tipo.Id, tipo.Codigo, tipo.Nome, tipo.Categoria,
            new ProtecaoDoDocumento(tipo.ClassificacaoProtecao, tipo.FinalidadeTratamento, tipo.HipoteseLegal),
            EscritaDaExigencia.AplicabilidadeDoToken(documentoInformado.Aplicabilidade()), documentoInformado.Obrigatorio(), documentoInformado.Consequencia(),
            partes.Condicoes, basesLegais, restricoes.Idade, restricoes.Formatos, documentoInformado.TamanhoMaximoBytes(),
            documentoInformado.EtapaId(), restricoes.Modelo, EscritaDaExigencia.FinalidadeDoToken(documentoInformado.Finalidade()), documentoInformado.FatosComprovados);
    }
}
