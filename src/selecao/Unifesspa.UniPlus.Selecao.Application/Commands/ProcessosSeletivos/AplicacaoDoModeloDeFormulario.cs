namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Aplica o modelo ao processo em memória: planeja a cópia (ver <see cref="PlanejamentoDaCopiaDoModelo"/>),
/// entrega-a ao agregado, que a confere e só então muda o processo, e copia o conjunto padrão de documentos
/// exigidos. Grava só quando tudo foi aceito.
/// </summary>
internal sealed class AplicacaoDoModeloDeFormulario(
    ProcessoSeletivo processo,
    ModeloFormularioView modelo,
    PrecondicaoIfMatch precondicao,
    CadastrosDaAplicacaoDoModelo cadastros,
    ISelecaoUnitOfWork unitOfWork)
{
    public async Task<Result<AplicacaoDeModeloDto>> AplicarAsync(CancellationToken cancellationToken)
    {
        Result<PlanoDaCopiaDoModelo> plano = await new PlanejamentoDaCopiaDoModelo(processo, modelo, cadastros)
            .PlanejarAsync(cancellationToken).ConfigureAwait(false);
        if (plano.IsFailure)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure(plano.Errors);
        }

        Result aplicada = processo.AplicarModeloDeFormulario(plano.Value!.Copia, precondicao);
        if (aplicada.IsFailure)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure([.. aplicada.Errors.Select(plano.Value.Indices.RecusaDoAgregado)]);
        }

        // O conjunto padrão de documentos entra no mesmo estado em memória e na mesma gravação: a recusa de
        // um documento desfaz a aplicação inteira.
        Result documentos = await new CopiaDosDocumentosDoModelo(processo, plano.Value.Copia.Finalidade, cadastros.Documentos)
            .CopiarAsync(modelo.Conteudo.DocumentosExigidos ?? [], plano.Value.Catalogo, precondicao, cancellationToken).ConfigureAwait(false);
        if (documentos.IsFailure)
        {
            return RecusaDosDocumentos(documentos);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<AplicacaoDeModeloDto>.Success(plano.Value.Relatorio.ParaDto(modelo.Finalidade));
    }

    // O formulário já mudou em memória, e o Wolverine grava o que estiver rastreado ao fim do handler,
    // inclusive quando ele devolve falha.
    private Result<AplicacaoDeModeloDto> RecusaDosDocumentos(Result documentos)
    {
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result<AplicacaoDeModeloDto>.ValidationFailure([.. documentos.Errors.Select(static e =>
            PosicaoNoModelo.NoModelo(e with { Field = CopiaDosDocumentosDoModelo.NoConjuntoDeDocumentos(e.Field) }))]);
    }
}
