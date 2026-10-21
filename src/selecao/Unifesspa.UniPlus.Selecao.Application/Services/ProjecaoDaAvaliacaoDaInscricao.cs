namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Queries.ProcessosSeletivos;

/// <summary>
/// A avaliação da inscrição na forma que o candidato recebe: as modalidades e os documentos das fases
/// do cronograma da versão que a inscrição fixou, com o endereço no acervo do modelo de cada exigência.
/// Única para a consulta, o rascunho, a leitura e o início da inscrição.
/// </summary>
public static class ProjecaoDaAvaliacaoDaInscricao
{
    /// <param name="faseId">A fase cujos documentos a resposta traz; nula, vêm todas as fases.</param>
    public static Result<AvaliacaoDaInscricaoDto> Projetar(
        ResultadoDaAvaliacaoDaInscricao resultado,
        ContextoDaVersaoFixada contexto,
        Guid processoSeletivoId,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        IReadOnlyList<ArquivoDaApresentacaoDto> arquivos,
        Guid? faseId = null)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);

        FaseCronograma[] fases = [.. contexto.Configuracao.Fases.Where(f => faseId is not { } pedida || f.Id == pedida)];
        if (faseId is not null && fases.Length == 0)
        {
            return Result<AvaliacaoDaInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.FaseNaoEncontrada,
                $"A fase {faseId} não está no cronograma da versão do formulário que a inscrição fixou."));
        }

        DocumentosDoAtoNoAcervo acervo = new(processoSeletivoId, contexto.AtoCriadorId, enderecoNoAcervo.De);
        return Result<AvaliacaoDaInscricaoDto>.Success(AvaliacaoDaInscricaoDto.De(resultado, fases, acervo.DoModelo, arquivos));
    }
}
