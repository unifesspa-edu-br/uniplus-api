namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Passo 1 do envio de um documento exigido na inscrição do dono do token: confere a exigência na
/// árvore congelada da versão da inscrição, cria o arquivo pendente e devolve a URL pré-assinada de PUT.
/// </summary>
/// <param name="ExigenciaId">O <c>DocumentoExigido.Id</c> da árvore congelada.</param>
/// <param name="OcorrenciaId">A ocorrência do grupo repetível a que o documento se refere; obrigatória quando a exigência se repete.</param>
/// <param name="ApresentacaoId">A apresentação a que o arquivo se junta (o verso depois da frente); sem ela, abre-se uma nova.</param>
/// <param name="TipoDeMidia"><c>application/pdf</c>, <c>image/jpeg</c> ou <c>image/png</c>.</param>
/// <param name="TamanhoBytes">O tamanho que o cliente vai enviar; conferido de novo na confirmação.</param>
public sealed record IniciarEnvioDeDocumentoDaInscricaoCommand(
    Guid ProcessoSeletivoId,
    Guid ExigenciaId,
    string? OcorrenciaId,
    Guid? ApresentacaoId,
    string? TipoDeMidia,
    long TamanhoBytes) : ICommand<Result<IniciarEnvioDeDocumentoDaInscricaoDto>>;
