namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Monta a exigência documental a partir dos dados informados.</summary>
internal sealed class MontagemDoDocumentoExigido(ContextoDaMontagemDasExigencias contexto)
{
    /// <param name="VinculoDoTipo">Recusa do tipo de documento como vínculo novo; não impede a montagem, para que a conferência dos dados da mesma exigência rode junto.</param>
    internal sealed record Partes(
        TipoDocumentoView Tipo, IReadOnlyList<CondicaoGatilho> Condicoes, EscritaDasRestricoesDoDocumento Restricoes, Result VinculoDoTipo);

    public async Task<Result<Partes>> ConferirReferenciasAsync(
        ItemDocumentoExigidoInput documento, RepeticaoDaRegra repeticao, CancellationToken cancellationToken)
    {
        Result<TipoDocumentoView> tipo = await TipoDeDocumentoAsync(documento, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyList<CondicaoGatilho>> condicoes = repeticao.Recusada
            ? Result<IReadOnlyList<CondicaoGatilho>>.Success([]) : Condicoes(documento, repeticao.Grupo);
        Result<EscritaDasRestricoesDoDocumento> restricoes = EscritaDasRestricoesDoDocumento.Resolver(documento, contexto.Modelos);
        return Reunir(tipo, condicoes, restricoes);
    }

    private Result<Partes> Reunir(Result<TipoDocumentoView> tipo, Result<IReadOnlyList<CondicaoGatilho>> condicoes,
        Result<EscritaDasRestricoesDoDocumento> restricoes)
    {
        List<FieldError> demais = [.. condicoes.Errors, .. restricoes.Errors];
        return tipo.Errors.Count + demais.Count > 0
            ? Result<Partes>.ValidationFailure([.. tipo.Errors, .. VinculoRecusadoDoTipo(tipo), .. demais])
            : Result<Partes>.Success(new(tipo.Value!, condicoes.Value!, restricoes.Value!, VinculoDoTipo(tipo.Value!)));
    }

    // Quando outra referência da mesma exigência é recusada, a recusa do vínculo novo do tipo vai junto, para a pessoa
    // ver todas as causas de uma vez.
    private IReadOnlyList<FieldError> VinculoRecusadoDoTipo(Result<TipoDocumentoView> tipo) =>
        tipo.IsSuccess ? VinculoDoTipo(tipo.Value!).Errors : [];

    public Result<DocumentoExigido> Criar(ItemDocumentoExigidoInput documento, Partes partes)
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
            : Result<TipoDocumentoView>.Success(tipo);
    }

    // O tipo sem a proteção aprovada só continua onde o processo já o exige: o vínculo novo é recusado, e
    // a publicação barra o que ficou pendente depois de vinculado (ADR-0153).
    private Result VinculoDoTipo(TipoDocumentoView tipo) =>
        !tipo.ProtecaoAprovada && contexto.Processo.DocumentosExigidos.All(d => d.TipoDocumentoOrigemId != tipo.Id)
            ? Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.TipoDocumentoNoVinculoComProtecaoPendente,
                $"O tipo de documento '{tipo.Nome}' aguarda a aprovação do Representante do controlador e não pode ser exigido agora. "
                + "Escolha outro tipo de documento ou retire a exigência, ou peça ao administrador do catálogo que envie a solicitação de aprovação, se ainda não a enviou."))
            : Result.Success();

    private Result<IReadOnlyList<CondicaoGatilho>> Condicoes(ItemDocumentoExigidoInput documentoInformado, string? grupoDaRepeticao) =>
        contexto.Fatos is { } fatos
            ? ConferenciaDasCondicoesDaExigencia.Resolver(documentoInformado, contexto.Processo, fatos, grupoDaRepeticao)
            : Result<IReadOnlyList<CondicaoGatilho>>.Success([]);

    private static Result<IReadOnlyList<DocumentoExigidoBaseLegal>> BasesLegais(ItemDocumentoExigidoInput documentoInformado) =>
        EscritaDasBasesLegais.Resolver(
            documentoInformado.BasesLegais(),
            static b => DocumentoExigidoBaseLegal.ValidarFormaBasica(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b)),
            static b => DocumentoExigidoBaseLegal.Criar(b.Referencia, EscritaDasBasesLegais.Abrangencia(b), EscritaDasBasesLegais.Situacao(b), b.Observacao).Value!);

    private Result<DocumentoExigido> CriarDocumento(
        ItemDocumentoExigidoInput documentoInformado, Partes partes, IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais)
    {
        TipoDocumentoView tipo = partes.Tipo;
        EscritaDasRestricoesDoDocumento restricoes = partes.Restricoes;
        // A forma da exigência já recusou o código do formulário ausente ou fora do formato.
        DocumentoExigidoDeclarado declarado = new(
            CopiaDosCadastrosDasVagas.TipoDeDocumento(tipo),
            new MomentoDaExigencia(documentoInformado.FaseId(), documentoInformado.EtapaId(), EscritaDaExigencia.FormularioDoCodigo(documentoInformado.FormularioCodigo())!),
            new AplicabilidadeDaExigencia(EscritaDaExigencia.AplicabilidadeDoToken(documentoInformado.Aplicabilidade()), partes.Condicoes),
            new IndeferimentoDoDocumento(documentoInformado.Obrigatorio(), documentoInformado.ConsequenciaDoDescumprimento(), basesLegais, documentoInformado.Indeferimento?.AbrePrazoParaReenvio ?? false),
            new RequisitosDoDocumento(restricoes.Formatos, documentoInformado.TamanhoMaximoBytes(), restricoes.Modelo, restricoes.Prazo, restricoes.Validade),
            documentoInformado.FatosComprovados);
        return contexto.Reconhecimento.Criar(documentoInformado.Chave, tipo.Codigo, declarado);
    }
}
