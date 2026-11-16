namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O resultado da validação dos campos independentes: as recusas, os tokens reconhecidos e os campos resolvidos.</summary>
internal sealed record CamposValidadosDaModalidade(
    IReadOnlyList<FieldError> Recusas,
    TokensReconhecidosDaModalidade Reconhecidos,
    CamposResolvidosDaModalidade Campos);
