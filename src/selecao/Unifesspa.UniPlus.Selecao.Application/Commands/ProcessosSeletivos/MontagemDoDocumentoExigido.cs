namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Monta a exigência documental a partir dos dados informados.</summary>
internal sealed class MontagemDoDocumentoExigido(ContextoDaMontagemDasExigencias contexto)
{
    public async Task<Result<DocumentoExigido>> MontarAsync(
        ItemDocumentoExigidoInput documentoInformado, string? grupoDaRepeticao, CancellationToken cancellationToken)
    {
        Result<TipoDocumentoView> tipo = await TipoDeDocumentoAsync(documentoInformado, cancellationToken).ConfigureAwait(false);
        return tipo.IsFailure
            ? Result<DocumentoExigido>.ValidationFailure(tipo.Errors)
            : MontarDoTipo(documentoInformado, tipo.Value!, grupoDaRepeticao);
    }

    private Result<DocumentoExigido> MontarDoTipo(ItemDocumentoExigidoInput documentoInformado, TipoDocumentoView tipo, string? grupoDaRepeticao)
    {
        Result<IReadOnlyList<CondicaoGatilho>> condicoes = Condicoes(documentoInformado, grupoDaRepeticao);
        if (condicoes.IsFailure)
        {
            return Result<DocumentoExigido>.ValidationFailure(condicoes.Errors);
        }

        Result<IReadOnlyList<DocumentoExigidoBaseLegal>> basesLegais = BasesLegais(documentoInformado);
        return basesLegais.IsFailure
            ? Result<DocumentoExigido>.ValidationFailure(basesLegais.Errors)
            : Criar(documentoInformado, tipo, condicoes.Value!, basesLegais.Value!);
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

    private Result<DocumentoExigido> Criar(
        ItemDocumentoExigidoInput documentoInformado, TipoDocumentoView tipo, IReadOnlyList<CondicaoGatilho> condicoes, IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais)
    {
        Result<EscritaDasRestricoesDoDocumento> restricoes = EscritaDasRestricoesDoDocumento.Resolver(documentoInformado, contexto.Modelos);
        return restricoes.IsFailure
            ? Result<DocumentoExigido>.ValidationFailure(restricoes.Errors)
            : DocumentoExigido.Criar(
                documentoInformado.FaseId(), tipo.Id, tipo.Codigo, tipo.Nome, tipo.Categoria,
                new ProtecaoDoDocumento(tipo.ClassificacaoProtecao, tipo.FinalidadeTratamento, tipo.HipoteseLegal),
                EscritaDaExigencia.AplicabilidadeDoToken(documentoInformado.Aplicabilidade()), documentoInformado.Obrigatorio(), documentoInformado.Consequencia(),
                condicoes, basesLegais, restricoes.Value!.Idade, restricoes.Value.Formatos, documentoInformado.TamanhoMaximoBytes(),
                documentoInformado.EtapaId(), restricoes.Value.Modelo, EscritaDaExigencia.FinalidadeDoToken(documentoInformado.Finalidade()), documentoInformado.FatosComprovados);
    }
}
