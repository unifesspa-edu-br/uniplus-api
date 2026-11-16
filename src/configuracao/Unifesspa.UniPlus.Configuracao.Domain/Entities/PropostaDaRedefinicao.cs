namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a alteração da definição propõe: os eixos a aplicar (o que há de válido na entrada, sobre o
/// que o fato já tem), as recusas do cadastro e se o ponto de resolução e a proteção são válidos —
/// o que a conferência das dependências precisa saber.
/// </summary>
internal readonly record struct PropostaDaRedefinicao(
    EixosDaDefinicao Propostos,
    IReadOnlyList<FieldError> Erros,
    bool PontoValido,
    bool ProtecaoValida);
