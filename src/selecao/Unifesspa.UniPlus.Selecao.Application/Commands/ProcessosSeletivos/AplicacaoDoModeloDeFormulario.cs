namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Aplica o modelo ao processo em memória: planeja a cópia (ver <see cref="PlanejamentoDaAplicacaoDoModelo"/>),
/// entrega-a ao agregado, que a confere e só então muda o processo, e copia o conjunto padrão de documentos
/// exigidos. Grava só quando tudo foi aceito.
/// </summary>
internal sealed class AplicacaoDoModeloDeFormulario(
    ProcessoSeletivo processo,
    FormularioProcesso formulario,
    ModeloFormularioView modelo,
    CadastrosDaAplicacaoDoModelo cadastros,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AplicacaoDeModeloDto>> AplicarAsync(CancellationToken cancellationToken)
    {
        Result<PlanoDaAplicacaoDoModelo> plano = await new PlanejamentoDaAplicacaoDoModelo(processo, formulario, modelo, cadastros)
            .PlanejarAsync(cancellationToken).ConfigureAwait(false);
        if (plano.IsFailure)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure(plano.Errors);
        }

        Result aplicada = processo.AplicarModeloDeFormulario(plano.Value!.Copia);
        return aplicada.IsFailure
            ? Result<AplicacaoDeModeloDto>.ValidationFailure([.. aplicada.Errors.Select(plano.Value.Indices.RecusaDoAgregado)])
            : await CopiarDocumentosEGravarAsync(plano.Value, cancellationToken).ConfigureAwait(false);
    }

    // O conjunto padrão de documentos entra no mesmo estado em memória e na mesma gravação: a recusa de
    // um documento desfaz a aplicação inteira.
    private async Task<Result<AplicacaoDeModeloDto>> CopiarDocumentosEGravarAsync(PlanoDaAplicacaoDoModelo plano, CancellationToken cancellationToken)
    {
        Result documentos = await new SelecaoDosDocumentosDoModelo(processo, formulario.Codigo, cadastros.Documentos)
            .CopiarAsync(modelo.Conteudo.ConjuntoDeDocumentos ?? new ConjuntoDeDocumentosDoModeloInput(null, null), plano.Catalogo, cancellationToken).ConfigureAwait(false);
        if (documentos.IsFailure)
        {
            return RecusaDosDocumentos(documentos);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<AplicacaoDeModeloDto>.Success(plano.Relatorio.ParaDto(formulario.Codigo));
    }

    // O formulário já mudou em memória, e o Wolverine grava o que estiver rastreado ao fim do handler,
    // inclusive quando ele devolve falha.
    private Result<AplicacaoDeModeloDto> RecusaDosDocumentos(Result documentos)
    {
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result<AplicacaoDeModeloDto>.ValidationFailure([.. documentos.Errors.Select(static e =>
            PosicaoNoModelo.NoModelo(SelecaoDosDocumentosDoModelo.NoConjuntoDeDocumentos(e)))]);
    }
}
